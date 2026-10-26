using Godot;

namespace km;

/// <summary>Silnice (dlážděné) a plány silnic (vytyčené čárkovaně).</summary>
public partial class RoadLayer : Node2D
{
    static readonly Color RoadBase = new(0.56f, 0.48f, 0.36f, 0.95f);
    static readonly Color Cobble = new(0.66f, 0.60f, 0.50f);
    static readonly Color PlanColor = new(0.95f, 0.90f, 0.70f, 0.85f);

    public override void _Draw()
    {
        var map = Main.Instance.Map;
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            var t = new Vector2I(x, y);
            if (map.Roads[x, y]) DrawRoad(map, t);
            else if (map.RoadPlans[x, y] != null) DrawPlan(map, t);
        }
    }

    void DrawRoad(GameMap map, Vector2I t)
    {
        uint h = (uint)(t.X * 73856093 ^ t.Y * 19349663);
        var c = map.TileCorners(t);
        var center = map.GroundToScreen(Iso.TileCenter(t));

        // měkký okraj: o kus větší, poloprůhledná vyšlapaná hlína pod dlažbou
        var halo = new Vector2[4];
        for (int i = 0; i < 4; i++) halo[i] = center + (c[i] - center) * 1.22f;
        DrawColoredPolygon(halo, new Color(0.42f, 0.34f, 0.22f, 0.28f));
        DrawColoredPolygon(c, Gfx.Shade(RoadBase, 0.95f + (h % 10) * 0.01f));

        // dlažební kostky
        for (int k = 0; k < 11; k++)
        {
            uint hk = h * (uint)(k + 3) * 2654435761u;
            float u = 0.1f + (hk % 100) / 100f * 0.8f;
            float v = 0.1f + (hk / 100 % 100) / 100f * 0.8f;
            var p = map.GroundToScreen(new Vector2(t.X + u, t.Y + v));
            float s = 0.82f + (hk / 10000 % 35) / 100f;
            float r = 3.2f + (hk / 1000 % 15) / 10f;
            Gfx.Ellipse(this, p + new Vector2(0.6f, 1f), r + 0.4f, r * 0.55f + 0.3f, new Color(0.1f, 0.07f, 0.04f, 0.3f));
            Gfx.Ellipse(this, p, r, r * 0.55f, Gfx.Shade(Cobble, s));
            Gfx.Ellipse(this, p + new Vector2(-r * 0.25f, -r * 0.15f), r * 0.45f, r * 0.22f, Gfx.Shade(Cobble, s * 1.15f));
        }
    }

    void DrawPlan(GameMap map, Vector2I t)
    {
        var c = map.TileCorners(t);
        DrawColoredPolygon(c, new Color(0.9f, 0.85f, 0.6f, 0.12f));
        for (int i = 0; i < 4; i++)
            DrawDashedLine(c[i], c[(i + 1) % 4], PlanColor, 1.5f, 4f);
    }
}
