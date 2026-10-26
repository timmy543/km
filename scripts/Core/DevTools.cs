using System.Linq;
using Godot;

namespace km;

/// <summary>
/// Ladicí přepínače z příkazové řádky (za `--`):
///   --seed=123          pevné semínko mapy
///   --demo              automaticky naplánuje dřevorubce, pilu, kamenolom a silnice
///   --timescale=4       zrychlení simulace
///   --zoom=1.0          počáteční přiblížení
///   --paths, --select   zobrazit cesty / vybrat poslední budovu
///   --screenshot=a.png  po --shot-after sekundách uloží snímek a ukončí hru
/// </summary>
public partial class DevTools : Node
{
    string? shotPath;
    double shotAfter = 3;
    bool demo, demoDone, select;
    ulong startMs;

    public override void _Ready()
    {
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a == "--demo") demo = true;
            else if (a == "--paths") Main.Instance.ShowPaths = true;
            else if (a == "--select") select = true;
            else if (a.StartsWith("--timescale=")) Engine.TimeScale = a["--timescale=".Length..].ToFloat();
            else if (a.StartsWith("--zoom=")) Main.Instance.Cam.ApplyZoom(a["--zoom=".Length..].ToFloat());
            else if (a.StartsWith("--screenshot=")) shotPath = a["--screenshot=".Length..];
            else if (a.StartsWith("--shot-after=")) shotAfter = a["--shot-after=".Length..].ToFloat();
        }
        startMs = Time.GetTicksMsec();
    }

    public override void _Process(double delta)
    {
        if (demo && !demoDone)
        {
            demoDone = true;
            RunDemo();
            if (select) Main.Instance.Selected = Main.Instance.Eco.Buildings.LastOrDefault();
        }
        if (shotPath != null && (Time.GetTicksMsec() - startMs) / 1000.0 >= shotAfter)
        {
            GetViewport().GetTexture().GetImage().SavePng(shotPath);
            GD.Print($"km: snímek uložen do {shotPath}");
            shotPath = null;
            GetTree().Quit();
        }
    }

    void RunDemo()
    {
        var m = Main.Instance;
        var store = m.Eco.NearestStore(m.Map.Center);
        if (store == null) return;
        foreach (var (def, offset) in new[]
                 {
                     (BuildingDef.Woodcutter, new Vector2I(-6, 3)),
                     (BuildingDef.Sawmill, new Vector2I(3, 5)),
                     (BuildingDef.Quarry, new Vector2I(6, -5)),
                 })
        {
            var b = PlaceNear(def, m.Map.Center + offset);
            if (b == null) continue;
            var path = m.Map.FindPath(b.Entrance, store.Entrance);
            if (path == null) continue;
            for (int i = 0; i < path.Count; i++)
            {
                // diagonální krok doplnit rohovou dlaždicí, ať je silnice souvislá
                if (i > 0 && path[i].X != path[i - 1].X && path[i].Y != path[i - 1].Y)
                {
                    var corner = new Vector2I(path[i].X, path[i - 1].Y);
                    if (!m.Map.CanPlaceRoad(corner)) corner = new Vector2I(path[i - 1].X, path[i].Y);
                    m.PlaceRoadPlan(corner);
                }
                m.PlaceRoadPlan(path[i]);
            }
        }
    }

    static Building? PlaceNear(BuildingDef def, Vector2I c)
    {
        var m = Main.Instance;
        for (int r = 0; r <= 10; r++)
        for (int y = c.Y - r; y <= c.Y + r; y++)
        for (int x = c.X - r; x <= c.X + r; x++)
        {
            var o = new Vector2I(x, y);
            if (m.Map.CanPlace(def, o, null)) return m.PlaceBuilding(def, o);
        }
        return null;
    }
}
