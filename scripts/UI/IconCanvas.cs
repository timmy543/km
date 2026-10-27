using Godot;

namespace km;

/// <summary>Ikona aplikace 256×256: zaoblený dřevěný štítek se zlatým lemem a skladem.</summary>
public partial class IconCanvas : Node2D
{
    public override void _Draw()
    {
        const float S = 256f;
        var rect = new Rect2(8, 8, S - 16, S - 16);
        DrawRounded(rect.Grow(2), 34, new Color(0.08f, 0.05f, 0.03f));
        DrawRounded(rect, 32, new Color(0.84f, 0.66f, 0.30f));
        DrawRounded(rect.Grow(-7), 26, new Color(0.30f, 0.19f, 0.10f));
        // prkna
        var inner = rect.Grow(-7);
        for (int i = 1; i < 5; i++)
        {
            float y = inner.Position.Y + inner.Size.Y * i / 5f;
            DrawLine(new Vector2(inner.Position.X + 6, y), new Vector2(inner.End.X - 6, y), new Color(0.2f, 0.12f, 0.06f), 2f);
        }
        // zelený kopeček a budova
        Gfx.Ellipse(this, new Vector2(128, 196), 96, 30, new Color(0.30f, 0.46f, 0.18f));
        IconPainter.Draw(this, IconKind.Building, BuildingDef.Storehouse, new Rect2(30, 34, 196, 176));
    }

    void DrawRounded(Rect2 r, float radius, Color c)
    {
        var pts = new System.Collections.Generic.List<Vector2>();
        var centers = new[]
        {
            new Vector2(r.End.X - radius, r.Position.Y + radius), new Vector2(r.End.X - radius, r.End.Y - radius),
            new Vector2(r.Position.X + radius, r.End.Y - radius), new Vector2(r.Position.X + radius, r.Position.Y + radius),
        };
        for (int k = 0; k < 4; k++)
            for (int i = 0; i <= 8; i++)
            {
                float a = -Mathf.Pi / 2f + (k + i / 8f) * Mathf.Pi / 2f;
                pts.Add(centers[k] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
        DrawColoredPolygon(pts.ToArray(), c);
    }
}
