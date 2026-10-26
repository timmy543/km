using Godot;

namespace km;

/// <summary>Procedurální bezešvé textury terénu (256×256) a šumová textura pro shadery.</summary>
public static class TerrainArt
{
    const int S = 256;

    public static ImageTexture Grass { get; private set; } = null!;
    public static ImageTexture Dirt { get; private set; } = null!;
    public static ImageTexture Rock { get; private set; } = null!;
    public static ImageTexture Sand { get; private set; } = null!;
    public static ImageTexture Noise { get; private set; } = null!;

    public static void Init(int seed)
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)seed + 1000 };
        Noise = MakeNoise(rng);
        Grass = MakeGrass(rng);
        Dirt = MakeDirt(rng);
        Rock = MakeRock(rng);
        Sand = MakeSand(rng);
    }

    static ImageTexture MakeNoise(RandomNumberGenerator rng)
    {
        var n = new TileableNoise(rng, 4, 8, 16, 32);
        var c = new PixelCanvas(S, S);
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float v = Mathf.Clamp((n.Sample((float)x / S, (float)y / S) - 0.5f) * 1.8f + 0.5f, 0f, 1f);
            c.Set(x, y, new Color(v, v, v));
        }
        return c.ToTexture(true);
    }

    static void Base(PixelCanvas c, TileableNoise n1, TileableNoise n2, Color a, Color b, Color tint, float tintAmt)
    {
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float u = (float)x / S, v = (float)y / S;
            float t = Mathf.Clamp((n1.Sample(u, v) - 0.5f) * 2.2f + 0.5f, 0f, 1f);
            var col = a.Lerp(b, t);
            float h = Mathf.Clamp((n2.Sample(u, v) - 0.45f) * 2.5f, 0f, 1f);
            col = col.Lerp(tint, h * tintAmt);
            c.Set(x, y, col);
        }
    }

    static ImageTexture MakeGrass(RandomNumberGenerator rng)
    {
        var c = new PixelCanvas(S, S);
        Base(c, new TileableNoise(rng, 4, 8, 16, 32, 64), new TileableNoise(rng, 4, 8),
            new Color(0.24f, 0.38f, 0.13f), new Color(0.40f, 0.52f, 0.20f), new Color(0.52f, 0.52f, 0.26f), 0.4f);

        // stébla – krátké tahy, tmavé u kořene a světlé na špičce
        for (int i = 0; i < 9000; i++)
        {
            var p = new Vector2(rng.Randf() * S, rng.Randf() * S);
            float ang = -Mathf.Pi / 2f + rng.RandfRange(-0.9f, 0.9f);
            float len = rng.RandfRange(2.5f, 6f);
            var tip = p + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * len;
            var bse = c.Get((int)p.X, (int)p.Y);
            c.Line(p, p.Lerp(tip, 0.5f), Gfx.Shade(bse, 0.78f), 0.55f);
            c.Line(p.Lerp(tip, 0.5f), tip, Gfx.Shade(bse, rng.RandfRange(1.1f, 1.3f)), 0.55f);
        }
        // tmavé stínky a drobné kvítky
        for (int i = 0; i < 700; i++)
            c.Multiply(rng.RandiRange(0, S - 1), rng.RandiRange(0, S - 1), 0.75f);
        var flowers = new[] { new Color(0.95f, 0.9f, 0.45f), new Color(0.95f, 0.95f, 0.9f), new Color(0.75f, 0.55f, 0.85f) };
        for (int i = 0; i < 60; i++)
            c.Disc(new Vector2(rng.Randf() * S, rng.Randf() * S), 0.9f, 0.9f, flowers[rng.RandiRange(0, 2)], 0.8f);
        return c.ToTexture(true);
    }

    static ImageTexture MakeDirt(RandomNumberGenerator rng)
    {
        var c = new PixelCanvas(S, S);
        Base(c, new TileableNoise(rng, 4, 8, 16, 32, 64), new TileableNoise(rng, 8, 16),
            new Color(0.44f, 0.34f, 0.21f), new Color(0.60f, 0.49f, 0.32f), new Color(0.40f, 0.42f, 0.22f), 0.3f);
        for (int i = 0; i < 1500; i++)
            c.Multiply(rng.RandiRange(0, S - 1), rng.RandiRange(0, S - 1), rng.RandfRange(0.7f, 1.2f));
        for (int i = 0; i < 260; i++)
        {
            float r = rng.RandfRange(1.2f, 2.8f);
            float g = rng.RandfRange(0.5f, 0.68f);
            var p = new Vector2(rng.Randf() * S, rng.Randf() * S);
            c.Disc(p + new Vector2(0.6f, 0.8f), r, r * 0.75f, new Color(0.2f, 0.15f, 0.1f), 0.45f);
            c.Disc(p, r, r * 0.75f, new Color(g, g * 0.95f, g * 0.85f), 0.95f, 0.3f);
        }
        return c.ToTexture(true);
    }

    static ImageTexture MakeRock(RandomNumberGenerator rng)
    {
        var c = new PixelCanvas(S, S);
        Base(c, new TileableNoise(rng, 3, 6, 12, 24, 48), new TileableNoise(rng, 8, 16, 32),
            new Color(0.44f, 0.42f, 0.39f), new Color(0.68f, 0.65f, 0.60f), new Color(0.48f, 0.52f, 0.36f), 0.3f);
        // praskliny – náhodné procházky s tmavou spárou a světlou hranou
        for (int i = 0; i < 40; i++)
        {
            var p = new Vector2(rng.Randf() * S, rng.Randf() * S);
            float ang = rng.Randf() * Mathf.Tau;
            int len = rng.RandiRange(10, 40);
            for (int k = 0; k < len; k++)
            {
                ang += rng.RandfRange(-0.5f, 0.5f);
                var q = p + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 1.5f;
                c.Line(p + new Vector2(0, 1), q + new Vector2(0, 1), new Color(0.82f, 0.80f, 0.75f), 0.35f);
                c.Line(p, q, new Color(0.22f, 0.21f, 0.2f), 0.7f);
                p = q;
            }
        }
        for (int i = 0; i < 120; i++)
        {
            float r = rng.RandfRange(1.5f, 3.5f);
            float g = rng.RandfRange(0.55f, 0.75f);
            c.Disc(new Vector2(rng.Randf() * S, rng.Randf() * S), r, r * 0.8f, new Color(g, g, g * 0.95f), 0.9f, 0.35f);
        }
        return c.ToTexture(true);
    }

    static ImageTexture MakeSand(RandomNumberGenerator rng)
    {
        var c = new PixelCanvas(S, S);
        Base(c, new TileableNoise(rng, 4, 8, 16, 32), new TileableNoise(rng, 4, 8),
            new Color(0.78f, 0.69f, 0.48f), new Color(0.90f, 0.82f, 0.60f), new Color(0.72f, 0.66f, 0.50f), 0.3f);
        var warp = new TileableNoise(rng, 4, 8);
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            // jemné vlnky; frekvence je celočíselná, aby textura zůstala bezešvá
            float w = warp.Sample((float)x / S, (float)y / S);
            float r = Mathf.Sin(((float)y / S * 18f + (float)x / S * 4f) * Mathf.Tau + w * 6f);
            c.Multiply(x, y, 1f + r * 0.015f + (rng.Randf() - 0.5f) * 0.07f);
        }
        return c.ToTexture(true);
    }
}
