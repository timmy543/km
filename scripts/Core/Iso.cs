using Godot;

namespace km;

/// <summary>
/// Převody mezi souřadnicemi mřížky (dlaždice, spojité) a obrazovkou.
/// Pohled jako v Knights and Merchants: čtvercová mřížka viděná šikmo shora,
/// výška terénu posouvá body svisle nahoru (to dělá GameMap.GroundToScreen).
/// </summary>
public static class Iso
{
    public const float Tile = 48f;

    public static Vector2 ToScreen(Vector2 g) => g * Tile;

    public static Vector2 ToGrid(Vector2 s) => s / Tile;

    public static Vector2 TileCenter(Vector2I t) => new(t.X + 0.5f, t.Y + 0.5f);
}
