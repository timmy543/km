using Godot;

namespace km;

public partial class TreeObj : Sprite2D
{
    public const float GrowTime = 90f;

    public Vector2I Tile;
    public Vector2 Jitter;       // posun v rámci dlaždice, ať les nevypadá jako šachovnice
    public float Growth = 1f;
    public Unit? ReservedBy;
    float baseScale = 1f;

    public bool IsMature => Growth >= 1f;

    public void Setup(Vector2I tile, SpriteArt art, bool mature, RandomNumberGenerator rng)
    {
        Tile = tile;
        Jitter = new Vector2(rng.RandfRange(-0.22f, 0.22f), rng.RandfRange(-0.22f, 0.22f));
        Texture = art.Tex;
        Material = ArtFactory.TreeMaterial;
        Offset = art.Offset;
        FlipH = rng.Randf() < 0.5f;
        baseScale = rng.RandfRange(0.85f, 1.1f);
        Growth = mature ? 1f : 0f;
        ApplyScale();
        SetProcess(!mature);
        UpdatePosition();
    }

    public void UpdatePosition() => Position = Main.Instance.Map.GroundToScreen(Iso.TileCenter(Tile) + Jitter);

    void ApplyScale()
    {
        float s = baseScale * (0.3f + 0.7f * Mathf.Clamp(Growth, 0f, 1f));
        Scale = new Vector2(s, s);
    }

    public override void _Process(double delta)
    {
        Growth += (float)delta / GrowTime;
        ApplyScale();
        if (IsMature) SetProcess(false);
    }
}
