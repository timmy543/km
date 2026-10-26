using Godot;

namespace km;

/// <summary>Nosič – převáží suroviny mezi budovami.</summary>
public partial class Serf : Unit
{
    public DeliveryJob? Job;

    public Serf() => Kind = UnitKind.Serf;

    protected override Color TunicColor => new(0.78f, 0.66f, 0.42f);
    protected override Color HatColor => new(0.62f, 0.50f, 0.28f);

    public void StartJob(DeliveryJob job)
    {
        ClearActions();
        Job = job;
        if (!job.PickedUp && job.Source != null)
        {
            Walk(job.Source.Entrance);
            Wait(0.4f);
            Do(PickUp);
        }
        Walk(job.Dest.Entrance);
        Wait(0.4f);
        Do(Drop);
    }

    bool PickUp()
    {
        var j = Job!;
        var src = j.Source!;
        int r = (int)j.Res;
        if (src.Removed || src.Stock[r] <= 0) return false;
        src.Stock[r]--;
        src.Outgoing[r]--;
        src.QueueRedraw();
        j.PickedUp = true;
        Carrying = j.Res;
        return true;
    }

    bool Drop()
    {
        var j = Job!;
        if (j.Dest.Removed) return false;
        j.Dest.Receive(j.Res);
        Carrying = null;
        Job = null;
        return true;
    }

    protected override void OnFailed()
    {
        var j = Job;
        Job = null;
        if (j == null) return;
        int r = (int)j.Res;
        if (!j.PickedUp && j.Source is { Removed: false } src) src.Outgoing[r]--;
        if (!j.Dest.Removed) j.Dest.Incoming[r]--;

        if (Carrying == null) return;
        if (j.IsReturn) Carrying = null; // ani do skladu se nedostal – náklad zahodí
        else Eco.ReturnCarried(this);
    }
}
