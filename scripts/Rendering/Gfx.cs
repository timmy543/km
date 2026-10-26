using Godot;

namespace km;

public static class Gfx
{
    static readonly Vector2[] UnitCircle = MakeCircle(20);

    static Vector2[] MakeCircle(int n)
    {
        var pts = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float a = Mathf.Tau * i / n;
            pts[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }
        return pts;
    }

    public static void Ellipse(CanvasItem ci, Vector2 c, float rx, float ry, Color col)
    {
        var pts = new Vector2[UnitCircle.Length];
        for (int i = 0; i < pts.Length; i++) pts[i] = c + UnitCircle[i] * new Vector2(rx, ry);
        ci.DrawColoredPolygon(pts, col);
    }

    public static Color Shade(Color c, float f) => new(c.R * f, c.G * f, c.B * f, c.A);

    public static Color Alpha(Color c, float a) => new(c.R, c.G, c.B, c.A * a);
}
