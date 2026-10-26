using Godot;

namespace km;

public enum IconKind { Trunk, Wood, Stone, Serf, Laborer, Craftsman, Road, Demolish, Building }

/// <summary>Vektorové ikony pro UI. Budovy kreslí stejný BuildingPainter jako ve hře, jen zmenšeně.</summary>
public static class IconPainter
{
    static readonly Color Outline = new(0.10f, 0.06f, 0.03f);

    public static IconKind ForResource(ResourceType r) => r switch
    {
        ResourceType.Trunk => IconKind.Trunk,
        ResourceType.Wood => IconKind.Wood,
        _ => IconKind.Stone,
    };

    public static void Draw(CanvasItem ci, IconKind kind, BuildingDef? def, Rect2 r)
    {
        var c = r.GetCenter();
        float s = Mathf.Min(r.Size.X, r.Size.Y);
        switch (kind)
        {
            case IconKind.Trunk:
                Log(ci, c + new Vector2(-0.02f, 0.16f) * s, s);
                Log(ci, c + new Vector2(0.06f, -0.08f) * s, s);
                break;
            case IconKind.Wood:
                for (int i = 0; i < 3; i++) Plank(ci, c + new Vector2(-0.04f * i, 0.2f - 0.17f * i) * s, s);
                break;
            case IconKind.Stone:
                Rock(ci, c + new Vector2(-0.16f, 0.12f) * s, 0.2f * s);
                Rock(ci, c + new Vector2(0.17f, 0.14f) * s, 0.17f * s);
                Rock(ci, c + new Vector2(0.02f, -0.1f) * s, 0.22f * s);
                break;
            case IconKind.Serf:
                Figure(ci, c, s, new Color(0.78f, 0.66f, 0.42f), new Color(0.62f, 0.50f, 0.28f));
                break;
            case IconKind.Laborer:
                Figure(ci, c, s, new Color(0.58f, 0.36f, 0.20f), new Color(0.85f, 0.70f, 0.30f));
                break;
            case IconKind.Craftsman:
                Figure(ci, c, s, new Color(0.28f, 0.48f, 0.22f), new Color(0.55f, 0.18f, 0.14f));
                break;
            case IconKind.Road:
                RoadIcon(ci, c, s);
                break;
            case IconKind.Demolish:
                DemolishIcon(ci, c, s);
                break;
            case IconKind.Building when def != null:
                BuildingIcon(ci, def, r);
                break;
        }
    }

    static void Log(CanvasItem ci, Vector2 p, float s)
    {
        var a = p + new Vector2(-0.32f, 0.08f) * s;
        var b = p + new Vector2(0.32f, -0.08f) * s;
        ci.DrawLine(a, b, Outline, 0.26f * s);
        ci.DrawLine(a, b, new Color(0.48f, 0.32f, 0.17f), 0.2f * s);
        ci.DrawLine(a + new Vector2(0, -0.05f * s), b + new Vector2(0, -0.05f * s), new Color(0.6f, 0.42f, 0.24f), 0.05f * s);
        ci.DrawCircle(a, 0.135f * s, Outline);
        ci.DrawCircle(a, 0.11f * s, new Color(0.86f, 0.70f, 0.45f));
        ci.DrawCircle(a, 0.05f * s, new Color(0.70f, 0.52f, 0.30f));
    }

    static void Plank(CanvasItem ci, Vector2 p, float s)
    {
        var rect = new Rect2(p + new Vector2(-0.36f, -0.07f) * s, new Vector2(0.72f, 0.14f) * s);
        ci.DrawRect(rect.Grow(0.025f * s), Outline);
        ci.DrawRect(rect, new Color(0.88f, 0.72f, 0.44f));
        ci.DrawRect(new Rect2(rect.Position, new Vector2(rect.Size.X, rect.Size.Y * 0.35f)), new Color(0.96f, 0.82f, 0.56f));
    }

    static void Rock(CanvasItem ci, Vector2 p, float r)
    {
        Gfx.Ellipse(ci, p, r * 1.1f, r * 0.85f, Outline);
        Gfx.Ellipse(ci, p, r, r * 0.75f, new Color(0.58f, 0.57f, 0.54f));
        Gfx.Ellipse(ci, p + new Vector2(-r * 0.25f, -r * 0.25f), r * 0.5f, r * 0.35f, new Color(0.78f, 0.77f, 0.73f));
    }

    static void Figure(CanvasItem ci, Vector2 c, float s, Color tunic, Color hat)
    {
        var head = c + new Vector2(0, -0.22f * s);
        var body = new[]
        {
            c + new Vector2(-0.24f, 0.38f) * s, c + new Vector2(0.24f, 0.38f) * s,
            c + new Vector2(0.17f, -0.06f) * s, c + new Vector2(-0.17f, -0.06f) * s,
        };
        ci.DrawPolyline(new[] { body[0], body[1], body[2], body[3], body[0] }, Outline, 0.06f * s);
        ci.DrawColoredPolygon(body, tunic);
        ci.DrawColoredPolygon(new[] { body[1], body[2], c + new Vector2(0.04f, -0.06f) * s, c + new Vector2(0.04f, 0.38f) * s }, Gfx.Shade(tunic, 0.8f));
        ci.DrawCircle(head, 0.15f * s, Outline);
        ci.DrawCircle(head, 0.12f * s, new Color(0.93f, 0.74f, 0.58f));
        ci.DrawColoredPolygon(new[]
        {
            head + new Vector2(-0.16f, -0.03f) * s, head + new Vector2(0.16f, -0.03f) * s,
            head + new Vector2(0.09f, -0.16f) * s, head + new Vector2(-0.09f, -0.16f) * s,
        }, hat);
    }

    static void RoadIcon(CanvasItem ci, Vector2 c, float s)
    {
        var pts = new[] { c + new Vector2(0, -0.26f) * s, c + new Vector2(0.46f, 0) * s, c + new Vector2(0, 0.26f) * s, c + new Vector2(-0.46f, 0) * s };
        ci.DrawColoredPolygon(pts, new Color(0.56f, 0.48f, 0.36f));
        ci.DrawPolyline(new[] { pts[0], pts[1], pts[2], pts[3], pts[0] }, Outline, 0.03f * s);
        var offs = new[] { new Vector2(-0.2f, 0), new Vector2(0.05f, -0.1f), new Vector2(0.2f, 0.02f), new Vector2(-0.02f, 0.1f), new Vector2(0.02f, -0.01f) };
        foreach (var o in offs)
        {
            Gfx.Ellipse(ci, c + o * s + new Vector2(0.01f, 0.015f) * s, 0.07f * s, 0.04f * s, new Color(0.2f, 0.15f, 0.1f, 0.5f));
            Gfx.Ellipse(ci, c + o * s, 0.065f * s, 0.036f * s, new Color(0.72f, 0.66f, 0.56f));
        }
    }

    static void DemolishIcon(CanvasItem ci, Vector2 c, float s)
    {
        // krumpáč přes kus zdi
        var wall = new Color(0.82f, 0.74f, 0.58f);
        ci.DrawRect(new Rect2(c + new Vector2(-0.34f, 0.0f) * s, new Vector2(0.3f, 0.32f) * s), Outline);
        ci.DrawRect(new Rect2(c + new Vector2(-0.32f, 0.02f) * s, new Vector2(0.26f, 0.28f) * s), wall);
        ci.DrawRect(new Rect2(c + new Vector2(0.04f, 0.14f) * s, new Vector2(0.18f, 0.18f) * s), Outline);
        ci.DrawRect(new Rect2(c + new Vector2(0.06f, 0.16f) * s, new Vector2(0.14f, 0.14f) * s), Gfx.Shade(wall, 0.8f));
        var a = c + new Vector2(-0.25f, 0.3f) * s;
        var b = c + new Vector2(0.3f, -0.3f) * s;
        ci.DrawLine(a, b, Outline, 0.1f * s);
        ci.DrawLine(a, b, new Color(0.55f, 0.36f, 0.18f), 0.06f * s);
        var head0 = b + new Vector2(-0.3f, -0.05f) * s;
        var head1 = b + new Vector2(0.08f, 0.28f) * s;
        ci.DrawLine(head0, head1, Outline, 0.11f * s);
        ci.DrawLine(head0, head1, new Color(0.66f, 0.68f, 0.72f), 0.07f * s);
        ci.DrawLine(c + new Vector2(-0.4f, -0.36f) * s, c + new Vector2(-0.12f, -0.08f) * s, new Color(0.85f, 0.2f, 0.15f), 0.07f * s);
        ci.DrawLine(c + new Vector2(-0.12f, -0.36f) * s, c + new Vector2(-0.4f, -0.08f) * s, new Color(0.85f, 0.2f, 0.15f), 0.07f * s);
    }

    static void BuildingIcon(CanvasItem ci, BuildingDef def, Rect2 r)
    {
        int w = def.Size.X, h = def.Size.Y;
        var n = Iso.ToScreen(Vector2.Zero);
        var e = Iso.ToScreen(new Vector2(w, 0));
        var s = Iso.ToScreen(new Vector2(w, h));
        var wc = Iso.ToScreen(new Vector2(0, h));
        float top = Mathf.Min(n.Y - def.WallH, (n.Y + wc.Y) / 2f - def.WallH - def.RoofH) - 3f;
        float bottom = s.Y + 2f;
        var box = new Rect2(wc.X - 2f, top, e.X - wc.X + 4f, bottom - top);
        float scale = Mathf.Min(r.Size.X / box.Size.X, r.Size.Y / box.Size.Y);
        var offset = r.GetCenter() - box.GetCenter() * scale;
        ci.DrawSetTransform(offset, 0f, new Vector2(scale, scale));
        BuildingPainter.Draw(ci, def, n, e, s, wc, null, 1f, 0f);
        ci.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}
