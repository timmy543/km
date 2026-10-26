using System.Collections.Generic;
using Godot;

namespace km;

/// <summary>Textura sprite a posun tak, aby počátek uzlu ležel na patě objektu.</summary>
public readonly record struct SpriteArt(ImageTexture Tex, Vector2 Offset);

/// <summary>
/// Procedurálně generovaná grafika – žádné externí assety.
/// Koruny stromů a kameny jsou skládané z "koulí" se stínováním podle světla zleva shora.
/// </summary>
public static class ArtFactory
{
    static readonly Vector3 ToLight = new Vector3(-0.5f, -0.75f, 0.65f).Normalized();

    public static ShaderMaterial TreeMaterial { get; private set; } = null!;
    public static SpriteArt[] Deciduous { get; private set; } = null!;
    public static SpriteArt[] Conifers { get; private set; } = null!;
    public static SpriteArt[] Stones { get; private set; } = null!;
    public static SpriteArt[] Decor { get; private set; } = null!;
    public static SpriteArt Stump { get; private set; }

    struct Blob
    {
        public Vector2 C;
        public float R, Sy;
        public Color Col;
    }

    /// <summary>Kývání korun ve větru – vrchní hrana sprite se posouvá, pata stojí. Fáze podle polohy.</summary>
    const string SwayShader = @"
shader_type canvas_item;
uniform float strength = 1.6;
void vertex() {
    float phase = MODEL_MATRIX[3].x * 0.011 + MODEL_MATRIX[3].y * 0.017;
    float h = 1.0 - UV.y;
    float gust = 0.7 + 0.3 * sin(TIME * 0.31 + MODEL_MATRIX[3].x * 0.002);
    VERTEX.x += sin(TIME * 1.25 + phase) * strength * gust * h * h;
}
";

    public static void Init(int seed)
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)seed };
        TreeMaterial = new ShaderMaterial { Shader = new Shader { Code = SwayShader } };

        var dec = new List<SpriteArt>();
        for (int i = 0; i < 5; i++) dec.Add(MakeDeciduous(rng));
        Deciduous = dec.ToArray();

        var con = new List<SpriteArt>();
        for (int i = 0; i < 3; i++) con.Add(MakeConifer(rng));
        Conifers = con.ToArray();

        var st = new List<SpriteArt>();
        for (int i = 0; i < 4; i++) st.Add(MakeStone(rng));
        Stones = st.ToArray();

        Stump = MakeStump();
        Decor = new[] { MakeBush(rng), MakeBush(rng), MakeFlowers(rng), MakeFlowers(rng), MakeTuft(rng), MakeTuft(rng), MakePebbles(rng) };
    }

    static SpriteArt Art(Image img, float baseY)
    {
        var tex = ImageTexture.CreateFromImage(img);
        return new SpriteArt(tex, new Vector2(0, img.GetHeight() / 2f - baseY));
    }

    // ---------------------------------------------------------------- malování do obrázku

    static void Over(Image img, int x, int y, Color c)
    {
        if (x < 0 || y < 0 || x >= img.GetWidth() || y >= img.GetHeight() || c.A <= 0f) return;
        var d = img.GetPixel(x, y);
        float a = c.A + d.A * (1f - c.A);
        if (a <= 0f) return;
        float r = (c.R * c.A + d.R * d.A * (1f - c.A)) / a;
        float g = (c.G * c.A + d.G * d.A * (1f - c.A)) / a;
        float b = (c.B * c.A + d.B * d.A * (1f - c.A)) / a;
        img.SetPixel(x, y, new Color(r, g, b, a));
    }

    static void Shadow(Image img, Vector2 c, float rx, float ry, float alpha)
    {
        for (int y = (int)(c.Y - ry) - 1; y <= (int)(c.Y + ry) + 1; y++)
        for (int x = (int)(c.X - rx) - 1; x <= (int)(c.X + rx) + 1; x++)
        {
            float dx = (x + 0.5f - c.X) / rx, dy = (y + 0.5f - c.Y) / ry;
            float d = dx * dx + dy * dy;
            if (d < 1f) Over(img, x, y, new Color(0.05f, 0.07f, 0.02f, alpha * (1f - d * d)));
        }
    }

    static void RenderBlobs(Image img, List<Blob> blobs, RandomNumberGenerator rng, float speckle)
    {
        foreach (var b in blobs)
        {
            float ry = b.R * b.Sy;
            for (int y = (int)(b.C.Y - ry) - 2; y <= (int)(b.C.Y + ry) + 2; y++)
            for (int x = (int)(b.C.X - b.R) - 2; x <= (int)(b.C.X + b.R) + 2; x++)
            {
                float dx = (x + 0.5f - b.C.X) / b.R, dy = (y + 0.5f - b.C.Y) / ry;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float cov = Mathf.Clamp((1f - d) * ry + 0.5f, 0f, 1f);
                if (cov <= 0f) continue;
                float dd = Mathf.Min(d, 0.999f);
                float nz = Mathf.Sqrt(1f - dd * dd);
                var n = new Vector3(dx, dy, nz).Normalized();
                float diff = Mathf.Max(0f, n.Dot(ToLight));
                float shade = 0.40f + 0.72f * diff;
                shade *= Mathf.Lerp(1f, 0.78f, Mathf.SmoothStep(0.72f, 1f, d));
                shade *= 1f + (rng.Randf() - 0.5f) * speckle;
                Over(img, x, y, new Color(b.Col.R * shade, b.Col.G * shade, b.Col.B * shade, cov));
            }
        }
    }

    static void Trunk(Image img, float cx, float baseY, float topY, float halfW, Color col)
    {
        for (int y = (int)topY; y <= (int)baseY; y++)
        {
            float hw = halfW + (y - topY) / (baseY - topY) * 1.4f;
            for (int x = (int)(cx - hw) - 1; x <= (int)(cx + hw) + 1; x++)
            {
                float rel = (x + 0.5f - (cx - hw)) / (2f * hw);
                float cov = Mathf.Clamp(Mathf.Min(x + 0.5f - (cx - hw), cx + hw - (x + 0.5f)) + 0.5f, 0f, 1f);
                if (cov <= 0f) continue;
                float shade = 1.15f - 0.65f * rel;
                Over(img, x, y, new Color(col.R * shade, col.G * shade, col.B * shade, cov));
            }
        }
    }

    // ---------------------------------------------------------------- objekty

    static SpriteArt MakeDeciduous(RandomNumberGenerator rng)
    {
        const int W = 64, H = 80;
        const float baseY = 72f, cx = 32f;
        var img = Image.CreateEmpty(W, H, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));
        Shadow(img, new Vector2(cx + 7, baseY - 1), 21f, 6.5f, 0.32f);
        Trunk(img, cx, baseY, 42f, 2.8f, new Color(0.42f, 0.29f, 0.17f));

        var greens = new[]
        {
            new Color(0.30f, 0.50f, 0.17f), new Color(0.36f, 0.56f, 0.19f),
            new Color(0.26f, 0.45f, 0.18f), new Color(0.42f, 0.54f, 0.18f),
        };
        var g = greens[rng.RandiRange(0, greens.Length - 1)];
        var center = new Vector2(cx, 33f);
        var blobs = new List<Blob>();
        int count = rng.RandiRange(8, 11);
        for (int i = 0; i < count; i++)
        {
            float a = rng.Randf() * Mathf.Tau, d = Mathf.Sqrt(rng.Randf()) * 13f;
            var c = center + new Vector2(Mathf.Cos(a) * d * 1.1f, Mathf.Sin(a) * d * 0.9f);
            float f = rng.RandfRange(0.88f, 1.12f);
            blobs.Add(new Blob { C = c, R = rng.RandfRange(8.5f, 12.5f), Sy = 0.95f, Col = new Color(g.R * f, g.G * f, g.B * f) });
        }
        blobs.Sort((p, q) => p.C.Y.CompareTo(q.C.Y));
        RenderBlobs(img, blobs, rng, 0.3f);
        return Art(img, baseY);
    }

    static SpriteArt MakeConifer(RandomNumberGenerator rng)
    {
        const int W = 64, H = 80;
        const float baseY = 72f, cx = 32f;
        var img = Image.CreateEmpty(W, H, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));
        Shadow(img, new Vector2(cx + 6, baseY - 1), 17f, 5.5f, 0.32f);
        Trunk(img, cx, baseY, 58f, 2.4f, new Color(0.38f, 0.26f, 0.16f));

        var col = new Color(0.16f, 0.34f, 0.20f) * rng.RandfRange(0.9f, 1.15f);
        // patra od spodu: (vrchol, základna, poloviční šířka)
        var tiers = new[] { (40f, 63f, 18f), (28f, 52f, 15f), (16f, 41f, 11.5f), (5f, 30f, 8f) };
        foreach (var (apex, bse, half) in tiers)
        {
            for (int y = (int)apex; y <= (int)bse + 2; y++)
            for (int x = (int)(cx - half) - 1; x <= (int)(cx + half) + 1; x++)
            {
                float jag = Mathf.Sin(x * 1.7f) * 1.3f;
                float bottom = bse + jag;
                float rel = (y + 0.5f - apex) / (bottom - apex);
                if (rel < 0f || rel > 1.05f) continue;
                float hw = half * Mathf.Min(rel, 1f);
                float ax = Mathf.Abs(x + 0.5f - cx);
                float cov = Mathf.Clamp(hw - ax + 0.5f, 0f, 1f) * Mathf.Clamp(bottom - (y + 0.5f) + 0.5f, 0f, 1f);
                if (cov <= 0f) continue;
                float u = (x + 0.5f - cx) / Mathf.Max(hw, 1f);
                float shade = (1.0f - 0.38f * u) * (0.72f + 0.38f * Mathf.Min(rel, 1f));
                shade *= 1f + (rng.Randf() - 0.5f) * 0.25f;
                Over(img, x, y, new Color(col.R * shade, col.G * shade, col.B * shade, cov));
            }
        }
        return Art(img, baseY);
    }

    static SpriteArt MakeStone(RandomNumberGenerator rng)
    {
        const int W = 48, H = 36;
        const float baseY = 28f;
        var img = Image.CreateEmpty(W, H, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));
        Shadow(img, new Vector2(27, baseY - 1), 19f, 6f, 0.3f);
        var blobs = new List<Blob>();
        int count = rng.RandiRange(3, 5);
        for (int i = 0; i < count; i++)
        {
            float g = rng.RandfRange(0.52f, 0.68f);
            blobs.Add(new Blob
            {
                C = new Vector2(24 + rng.RandfRange(-9f, 9f), 20 + rng.RandfRange(-4f, 4f)),
                R = rng.RandfRange(6.5f, 11f), Sy = 0.75f,
                Col = new Color(g, g * 0.97f, g * 0.9f),
            });
        }
        blobs.Sort((p, q) => p.C.Y.CompareTo(q.C.Y));
        RenderBlobs(img, blobs, rng, 0.12f);
        return Art(img, baseY);
    }

    static SpriteArt MakeStump()
    {
        var rng = new RandomNumberGenerator { Seed = 7 };
        var img = Image.CreateEmpty(28, 20, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));
        Shadow(img, new Vector2(16, 14), 10f, 3.5f, 0.3f);
        RenderBlobs(img, new List<Blob>
        {
            new() { C = new Vector2(14, 12), R = 6.5f, Sy = 0.6f, Col = new Color(0.45f, 0.31f, 0.18f) },
            new() { C = new Vector2(14, 10), R = 5f, Sy = 0.45f, Col = new Color(0.82f, 0.66f, 0.42f) },
        }, rng, 0.1f);
        return Art(img, 14f);
    }

    static SpriteArt MakeBush(RandomNumberGenerator rng)
    {
        var img = Image.CreateEmpty(32, 24, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));
        Shadow(img, new Vector2(18, 19), 12f, 3.5f, 0.28f);
        var blobs = new List<Blob>();
        for (int i = 0; i < 5; i++)
            blobs.Add(new Blob
            {
                C = new Vector2(16 + rng.RandfRange(-6f, 6f), 14 + rng.RandfRange(-3f, 2f)),
                R = rng.RandfRange(4f, 6.5f), Sy = 0.85f,
                Col = new Color(0.30f, 0.50f, 0.18f) * rng.RandfRange(0.85f, 1.1f),
            });
        blobs.Sort((p, q) => p.C.Y.CompareTo(q.C.Y));
        RenderBlobs(img, blobs, rng, 0.3f);
        return Art(img, 19f);
    }

    static SpriteArt MakeFlowers(RandomNumberGenerator rng)
    {
        var img = Image.CreateEmpty(28, 18, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));
        var blobs = new List<Blob>();
        for (int i = 0; i < 4; i++)
            blobs.Add(new Blob { C = new Vector2(14 + rng.RandfRange(-6f, 6f), 12 + rng.RandfRange(-2f, 2f)), R = rng.RandfRange(2.5f, 3.5f), Sy = 0.8f, Col = new Color(0.36f, 0.56f, 0.2f) });
        var petals = new[] { new Color(0.95f, 0.85f, 0.3f), new Color(0.95f, 0.95f, 0.9f), new Color(0.8f, 0.3f, 0.3f), new Color(0.6f, 0.45f, 0.85f) };
        var pc = petals[rng.RandiRange(0, petals.Length - 1)];
        for (int i = 0; i < 6; i++)
            blobs.Add(new Blob { C = new Vector2(14 + rng.RandfRange(-8f, 8f), 10 + rng.RandfRange(-3f, 3f)), R = 1.6f, Sy = 1f, Col = pc });
        RenderBlobs(img, blobs, rng, 0.15f);
        return Art(img, 14f);
    }

    static SpriteArt MakeTuft(RandomNumberGenerator rng)
    {
        var img = Image.CreateEmpty(24, 18, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));
        for (int k = 0; k < 10; k++)
        {
            var from = new Vector2(12 + rng.RandfRange(-5f, 5f), 15f);
            var to = from + new Vector2(rng.RandfRange(-5f, 5f), -rng.RandfRange(6f, 12f));
            var col = new Color(0.34f, 0.55f, 0.18f) * rng.RandfRange(0.8f, 1.25f);
            for (float t = 0; t <= 1f; t += 0.05f)
            {
                var p = from.Lerp(to, t);
                Over(img, (int)p.X, (int)p.Y, new Color(col.R, col.G, col.B, 1f - t * 0.3f));
            }
        }
        return Art(img, 15f);
    }

    static SpriteArt MakePebbles(RandomNumberGenerator rng)
    {
        var img = Image.CreateEmpty(28, 18, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));
        var blobs = new List<Blob>();
        for (int i = 0; i < 4; i++)
        {
            float g = rng.RandfRange(0.55f, 0.7f);
            blobs.Add(new Blob { C = new Vector2(14 + rng.RandfRange(-7f, 7f), 11 + rng.RandfRange(-2f, 2f)), R = rng.RandfRange(2.5f, 4f), Sy = 0.7f, Col = new Color(g, g, g * 0.92f) });
        }
        RenderBlobs(img, blobs, rng, 0.1f);
        return Art(img, 13f);
    }
}
