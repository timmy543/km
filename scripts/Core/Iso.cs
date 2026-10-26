using Godot;

namespace km;

/// <summary>
/// Převody mezi souřadnicemi mřížky (dlaždice, spojité) a obrazovkou.
/// Klasická 2:1 izometrie – dlaždice je kosočtverec 64×32 px.
/// </summary>
public static class Iso
{
    public const float TileW = 64f;
    public const float TileH = 32f;
    public const float HalfW = TileW / 2f;
    public const float HalfH = TileH / 2f;

    public static Vector2 ToScreen(Vector2 g) => new((g.X - g.Y) * HalfW, (g.X + g.Y) * HalfH);

    public static Vector2 ToGrid(Vector2 s) =>
        new((s.X / HalfW + s.Y / HalfH) * 0.5f, (s.Y / HalfH - s.X / HalfW) * 0.5f);

    public static Vector2 TileCenter(Vector2I t) => new(t.X + 0.5f, t.Y + 0.5f);
}
