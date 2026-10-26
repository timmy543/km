using Godot;

namespace km;

/// <summary>
/// Rychlé kreslení do pole floatů s obtékáním přes okraje (pro bezešvé textury).
/// Do Image se převádí najednou – SetPixel po pixelu by byl u 256×256 pomalý.
/// </summary>
public sealed class PixelCanvas
{
    public readonly int W, H;
    readonly float[] px;

    public PixelCanvas(int w, int h)
    {
        W = w;
        H = h;
        px = new float[w * h * 4];
    }

    int Index(int x, int y)
    {
        x = ((x % W) + W) % W;
        y = ((y % H) + H) % H;
        return (y * W + x) * 4;
    }

    public Color Get(int x, int y)
    {
        int i = Index(x, y);
        return new Color(px[i], px[i + 1], px[i + 2], px[i + 3]);
    }

    public void Set(int x, int y, Color c)
    {
        int i = Index(x, y);
        px[i] = c.R;
        px[i + 1] = c.G;
        px[i + 2] = c.B;
        px[i + 3] = c.A;
    }

    /// <summary>Míchání "přes" na neprůhledný podklad.</summary>
    public void Blend(int x, int y, Color c, float a)
    {
        int i = Index(x, y);
        px[i] += (c.R - px[i]) * a;
        px[i + 1] += (c.G - px[i + 1]) * a;
        px[i + 2] += (c.B - px[i + 2]) * a;
        px[i + 3] = 1f;
    }

    public void Multiply(int x, int y, float f)
    {
        int i = Index(x, y);
        px[i] *= f;
        px[i + 1] *= f;
        px[i + 2] *= f;
    }

    public void Line(Vector2 a, Vector2 b, Color c, float alpha)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt(a.DistanceTo(b) * 2f));
        for (int s = 0; s <= steps; s++)
        {
            var p = a.Lerp(b, (float)s / steps);
            Blend(Mathf.FloorToInt(p.X), Mathf.FloorToInt(p.Y), c, alpha);
        }
    }

    public void Disc(Vector2 c, float rx, float ry, Color col, float alpha, float edgeShade = 0f)
    {
        for (int y = Mathf.FloorToInt(c.Y - ry) - 1; y <= Mathf.CeilToInt(c.Y + ry) + 1; y++)
        for (int x = Mathf.FloorToInt(c.X - rx) - 1; x <= Mathf.CeilToInt(c.X + rx) + 1; x++)
        {
            float dx = (x + 0.5f - c.X) / rx, dy = (y + 0.5f - c.Y) / ry;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d >= 1f) continue;
            // světlo zleva shora, spodní okraj tmavší
            float shade = 1f + (-dx - dy) * 0.18f - Mathf.SmoothStep(0.6f, 1f, d) * edgeShade;
            Blend(x, y, Gfx.Shade(col, shade), alpha * Mathf.Clamp((1f - d) * Mathf.Min(rx, ry) + 0.3f, 0f, 1f));
        }
    }

    public ImageTexture ToTexture(bool mipmaps)
    {
        var bytes = new byte[W * H * 4];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = (byte)Mathf.Clamp(Mathf.RoundToInt(px[i] * 255f), 0, 255);
        var img = Image.CreateFromData(W, H, false, Image.Format.Rgba8, bytes);
        if (mipmaps) img.GenerateMipmaps();
        return ImageTexture.CreateFromImage(img);
    }
}

/// <summary>Bezešvý hodnotový šum (fBm) – periodický na jednotkovém čtverci.</summary>
public sealed class TileableNoise
{
    readonly float[][,] lattices;
    readonly int[] sizes;

    public TileableNoise(RandomNumberGenerator rng, params int[] octaveSizes)
    {
        sizes = octaveSizes;
        lattices = new float[octaveSizes.Length][,];
        for (int o = 0; o < octaveSizes.Length; o++)
        {
            int n = octaveSizes[o];
            var l = new float[n, n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                l[x, y] = rng.Randf();
            lattices[o] = l;
        }
    }

    /// <summary>u, v v [0,1); výsledek zhruba 0..1, vyšší oktávy mají menší váhu.</summary>
    public float Sample(float u, float v)
    {
        float sum = 0, norm = 0, amp = 1;
        for (int o = 0; o < sizes.Length; o++)
        {
            sum += Octave(u, v, lattices[o], sizes[o]) * amp;
            norm += amp;
            amp *= 0.55f;
        }
        return sum / norm;
    }

    static float Octave(float u, float v, float[,] l, int n)
    {
        float x = u * n, y = v * n;
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float fx = x - xi, fy = y - yi;
        int x0 = ((xi % n) + n) % n, y0 = ((yi % n) + n) % n;
        int x1 = (x0 + 1) % n, y1 = (y0 + 1) % n;
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);
        return Mathf.Lerp(Mathf.Lerp(l[x0, y0], l[x1, y0], fx), Mathf.Lerp(l[x0, y1], l[x1, y1], fx), fy);
    }
}
