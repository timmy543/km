using System.Collections.Generic;
using System.Linq;
using Godot;

namespace km;

public sealed class DeliveryJob
{
    public Building? Source;
    public Building Dest = null!;
    public ResourceType Res;
    public bool PickedUp;
    public bool IsReturn;   // vracení neodevzdaného nákladu do skladu
}

/// <summary>
/// Centrální dispečer: páruje poptávku (staveniště, dílny) s nabídkou (sklady, výrobny)
/// a přiděluje volné nosiče a stavitele. Rezervace zabraňují dvojímu odvozu téhož kusu.
/// </summary>
public sealed class Economy
{
    public readonly List<Building> Buildings = new();
    public readonly List<IWorkOrder> Orders = new();
    public readonly List<Serf> Serfs = new();
    public readonly List<Laborer> Laborers = new();
    public readonly List<Worker> Workers = new();

    float timer;

    public IEnumerable<Unit> AllUnits => Serfs.Cast<Unit>().Concat(Laborers).Concat(Workers);

    public void Update(float dt)
    {
        timer += dt;
        if (timer < 0.25f) return;
        timer = 0f;
        Orders.RemoveAll(o => o.IsFinished);
        DispatchLaborers();
        DispatchSerfs();
    }

    public Building? NearestStore(Vector2I from) =>
        Buildings.Where(b => b.Def.IsStore && b.State == BuildState.Done && !b.Removed)
            .MinBy(b => (b.Entrance - from).LengthSquared());

    public int TotalInStores(ResourceType r) =>
        Buildings.Where(b => b.Def.IsStore && b.State == BuildState.Done).Sum(b => b.Stock[(int)r]);

    // ---------------------------------------------------------------- stavitelé

    void DispatchLaborers()
    {
        var idle = Laborers.Where(l => l.Order == null).ToList();
        if (idle.Count == 0) return;
        double now = Time.GetTicksMsec() / 1000.0;
        foreach (var o in Orders)
        {
            if (o.RetryAt > now) continue;
            while (o.Builders.Count < o.MaxBuilders && idle.Count > 0)
            {
                var l = idle.MinBy(u => u.GridPos.DistanceSquaredTo(Iso.TileCenter(o.WorkTile)))!;
                idle.Remove(l);
                l.AssignOrder(o);
            }
            if (idle.Count == 0) return;
        }
    }

    // ---------------------------------------------------------------- nosiči

    void DispatchSerfs()
    {
        var idle = Serfs.Where(s => s.Job == null).ToList();
        if (idle.Count == 0) return;

        // 1) staveniště v pořadí, v jakém byla zadána
        foreach (var b in Buildings)
        {
            if (b.Removed || b.State == BuildState.Done) continue;
            foreach (var r in Res.BuildMaterials)
            {
                int need = b.ConstructionNeed(r);
                for (int i = 0; i < need; i++)
                {
                    if (idle.Count == 0) return;
                    if (!Supply(r, b, idle)) break;
                }
            }
        }

        // 2) vstupy dílen
        foreach (var b in Buildings)
        {
            if (b.Removed || b.State != BuildState.Done) continue;
            foreach (var r in b.Def.Inputs)
            {
                int need = b.Def.MaxStock - b.Stock[(int)r] - b.Incoming[(int)r];
                for (int i = 0; i < need; i++)
                {
                    if (idle.Count == 0) return;
                    if (!Supply(r, b, idle)) break;
                }
            }
        }

        // 3) zbytek výroby do skladu
        foreach (var b in Buildings)
        {
            if (b.Removed || b.State != BuildState.Done || b.Def.IsStore) continue;
            foreach (var r in b.Def.Outputs)
            {
                int avail = b.Stock[(int)r] - b.Outgoing[(int)r];
                var store = NearestStore(b.Entrance);
                if (store == null) break;
                for (int i = 0; i < avail; i++)
                {
                    if (idle.Count == 0) return;
                    Assign(b, store, r, idle);
                }
            }
        }
    }

    bool Supply(ResourceType r, Building dest, List<Serf> idle)
    {
        Building? best = null;
        int bd = int.MaxValue;
        foreach (var s in Buildings)
        {
            if (s == dest || s.Offer(r) <= 0) continue;
            int d = (s.Entrance - dest.Entrance).LengthSquared();
            if (d < bd) { bd = d; best = s; }
        }
        if (best == null) return false;
        Assign(best, dest, r, idle);
        return true;
    }

    void Assign(Building src, Building dst, ResourceType r, List<Serf> idle)
    {
        var target = Iso.TileCenter(src.Entrance);
        var serf = idle.MinBy(s => s.GridPos.DistanceSquaredTo(target))!;
        idle.Remove(serf);
        src.Outgoing[(int)r]++;
        dst.Incoming[(int)r]++;
        serf.StartJob(new DeliveryJob { Source = src, Dest = dst, Res = r });
    }

    /// <summary>Nosič nemohl náklad doručit – odnese ho do nejbližšího skladu.</summary>
    public void ReturnCarried(Serf s)
    {
        if (s.Carrying is not { } r) return;
        var store = NearestStore(s.Tile);
        if (store == null)
        {
            s.Carrying = null;
            return;
        }
        store.Incoming[(int)r]++;
        s.StartJob(new DeliveryJob { Dest = store, Res = r, PickedUp = true, IsReturn = true });
    }
}
