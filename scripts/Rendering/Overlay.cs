using System.Collections.Generic;
using Godot;

namespace km;

/// <summary>Kurzor, náhled umístění budovy, výběr a ladicí zobrazení cest (klávesa P).</summary>
public partial class Overlay : Node2D
{
    static readonly Color Ok = new(0.35f, 0.9f, 0.35f);
    static readonly Color Bad = new(0.95f, 0.25f, 0.2f);
    readonly List<(Vector2I tile, bool ok)> placement = new();
    float time;

    public override void _Process(double delta)
    {
        time += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var m = Main.Instance;
        var map = m.Map;

        if (m.Selected is { Removed: false } sel) DrawSelection(map, sel);
        if (m.ShowPaths) DrawPaths(map, m);

        var t = m.HoverTile;
        if (!map.InBounds(t)) return;

        switch (m.Tool)
        {
            case ToolMode.Build when m.BuildDef != null:
                var def = m.BuildDef;
                var origin = Main.BuildOrigin(def, t);
                placement.Clear();
                bool ok = map.CanPlace(def, origin, placement);
                foreach (var (tile, good) in placement)
                    if (map.InBounds(tile)) Tile(map, tile, good ? Ok : Bad, 0.3f);
                Vector2 P(int x, int y) => map.GroundToScreen(new Vector2(x, y));
                int ox = origin.X, oy = origin.Y, w = def.Size.X, h = def.Size.Y;
                BuildingPainter.Draw(this, def, P(ox, oy), P(ox + w, oy), P(ox + w, oy + h), P(ox, oy + h), null, ok ? 0.6f : 0.3f, time);
                if (def.GatherRadius > 0) DrawRadius(map, Building.EntranceFor(def, origin), def.GatherRadius);
                break;
            case ToolMode.Road:
                Tile(map, t, map.CanPlaceRoad(t) ? Ok : Bad, 0.35f);
                break;
            case ToolMode.Demolish:
                Tile(map, t, Bad, 0.4f);
                break;
            default:
                var c = map.TileCorners(t);
                DrawPolyline(new[] { c[0], c[1], c[2], c[3], c[0] }, new Color(1, 1, 1, 0.45f), 1.5f);
                break;
        }
    }

    void Tile(GameMap map, Vector2I t, Color col, float fill)
    {
        var c = map.TileCorners(t);
        DrawColoredPolygon(c, Gfx.Alpha(col, fill));
        DrawPolyline(new[] { c[0], c[1], c[2], c[3], c[0] }, Gfx.Alpha(col, 0.9f), 1.5f);
    }

    /// <summary>Dosah řemeslníka – kruh v mřížce, na obrazovce tedy izometrická elipsa po terénu.</summary>
    void DrawRadius(GameMap map, Vector2I center, int r)
    {
        const int n = 64;
        var c = Iso.TileCenter(center);
        var pts = new Vector2[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float a = Mathf.Tau * i / n;
            pts[i] = map.GroundToScreen(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
        }
        DrawPolyline(pts, new Color(1f, 0.95f, 0.7f, 0.7f), 2f);
    }

    void DrawSelection(GameMap map, Building b)
    {
        if (b.Def.GatherRadius > 0 && b.State == BuildState.Done) DrawRadius(map, b.Entrance, b.Def.GatherRadius);
        var col = new Color(1f, 0.85f, 0.3f, 0.9f);
        foreach (var t in b.Footprint())
        {
            var c = map.TileCorners(t);
            DrawColoredPolygon(c, Gfx.Alpha(col, 0.12f));
        }
        int x = b.Origin.X, y = b.Origin.Y, w = b.Def.Size.X, h = b.Def.Size.Y;
        var outline = new[]
        {
            map.GroundToScreen(new Vector2(x, y)), map.GroundToScreen(new Vector2(x + w, y)),
            map.GroundToScreen(new Vector2(x + w, y + h)), map.GroundToScreen(new Vector2(x, y + h)),
            map.GroundToScreen(new Vector2(x, y)),
        };
        DrawPolyline(outline, col, 2f);
        var e = map.GroundToScreen(Iso.TileCenter(b.Entrance));
        Gfx.Ellipse(this, e, 10f, 5f, Gfx.Alpha(col, 0.35f));
    }

    void DrawPaths(GameMap map, Main m)
    {
        foreach (var u in m.Eco.AllUnits)
        {
            if (u.ActiveWalk is not { Path: { } path } walk || !u.Visible) continue;
            var col = u.Kind switch
            {
                UnitKind.Serf => new Color(1f, 0.9f, 0.4f, 0.8f),
                UnitKind.Laborer => new Color(1f, 0.55f, 0.25f, 0.8f),
                _ => new Color(0.5f, 0.85f, 1f, 0.8f),
            };
            var pts = new List<Vector2> { u.Position };
            for (int i = walk.Index; i < path.Count; i++)
                pts.Add(map.GroundToScreen(Iso.TileCenter(path[i])));
            if (pts.Count < 2) continue;
            DrawPolyline(pts.ToArray(), col, 2f);
            DrawCircle(pts[^1], 3.5f, col);
        }
    }
}
