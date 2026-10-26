using Godot;

namespace km;

public partial class StoneObj : Sprite2D
{
    public Vector2I Tile;
    public int Amount, MaxAmount;

    public void Setup(Vector2I tile, SpriteArt art, int amount, RandomNumberGenerator rng)
    {
        Tile = tile;
        Amount = MaxAmount = amount;
        Texture = art.Tex;
        Offset = art.Offset;
        FlipH = rng.Randf() < 0.5f;
        ApplyScale();
        UpdatePosition();
    }

    public void UpdatePosition() => Position = Main.Instance.Map.GroundToScreen(Iso.TileCenter(Tile));

    public void ApplyScale()
    {
        float s = 0.55f + 0.5f * Amount / MaxAmount;
        Scale = new Vector2(s, s);
    }
}
