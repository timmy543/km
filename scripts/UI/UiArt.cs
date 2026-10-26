using Godot;

namespace km;

/// <summary>
/// Procedurální grafika rozhraní: dřevěné panely a tlačítka jako 9-patch textury
/// (střed je periodický, takže se dá dlaždicovat bez švů), písmo a téma.
/// </summary>
public static class UiArt
{
    public static readonly Color Parchment = new(0.97f, 0.90f, 0.74f);
    public static readonly Color Gold = new(0.90f, 0.72f, 0.34f);
    public static readonly Color Ink = new(0.22f, 0.14f, 0.07f);

    public static Font Serif { get; private set; } = null!;
    public static Font SerifBold { get; private set; } = null!;
    public static Theme Theme { get; private set; } = null!;

    public static StyleBoxTexture Panel { get; private set; } = null!;
    public static StyleBoxTexture Bar { get; private set; } = null!;
    public static StyleBoxTexture Inset { get; private set; } = null!;

    public static void Init()
    {
        var names = new[] { "Palatino Linotype", "Book Antiqua", "Georgia", "Cambria", "serif" };
        Serif = new SystemFont { FontNames = names, FontWeight = 500 };
        SerifBold = new SystemFont { FontNames = names, FontWeight = 700 };

        var rng = new RandomNumberGenerator { Seed = 4242 };
        var wood = new Color(0.34f, 0.22f, 0.12f);
        Panel = Box(WoodBox(96, 16, wood, frame: true, gold: true, rivets: true, inset: false, rng), 16, 12);
        Bar = Box(WoodBox(64, 8, wood, frame: true, gold: true, rivets: false, inset: false, rng), 8, 6);
        Inset = Box(WoodBox(48, 6, new Color(0.16f, 0.11f, 0.07f), frame: true, gold: false, rivets: false, inset: true, rng), 6, 4);

        var btnWood = new Color(0.44f, 0.30f, 0.17f);
        var normal = Box(WoodBox(48, 7, btnWood, frame: true, gold: false, rivets: false, inset: false, rng), 7, 6);
        var hover = Box(WoodBox(48, 7, Gfx.Shade(btnWood, 1.18f), frame: true, gold: true, rivets: false, inset: false, rng), 7, 6);
        var pressed = Box(WoodBox(48, 7, Gfx.Shade(btnWood, 0.8f), frame: true, gold: true, rivets: false, inset: true, rng), 7, 6);
        var disabled = Box(WoodBox(48, 7, Gfx.Shade(btnWood, 0.6f), frame: true, gold: false, rivets: false, inset: false, rng), 7, 6);

        var t = new Theme { DefaultFont = Serif, DefaultFontSize = 15 };
        t.SetStylebox("panel", "PanelContainer", Panel);
        t.SetStylebox("normal", "Button", normal);
        t.SetStylebox("hover", "Button", hover);
        t.SetStylebox("pressed", "Button", pressed);
        t.SetStylebox("hover_pressed", "Button", pressed);
        t.SetStylebox("disabled", "Button", disabled);
        t.SetStylebox("focus", "Button", new StyleBoxEmpty());
        foreach (var c in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" })
            t.SetColor(c, "Button", Parchment);
        t.SetColor("font_outline_color", "Button", new Color(0.08f, 0.05f, 0.02f));
        t.SetConstant("outline_size", "Button", 3);

        t.SetColor("font_color", "Label", Parchment);
        t.SetColor("font_outline_color", "Label", new Color(0.08f, 0.05f, 0.02f));
        t.SetConstant("outline_size", "Label", 3);

        // pergamenové tooltipy
        var tip = new StyleBoxFlat { BgColor = new Color(0.93f, 0.86f, 0.68f), BorderColor = new Color(0.45f, 0.30f, 0.14f) };
        tip.SetBorderWidthAll(2);
        tip.SetCornerRadiusAll(3);
        tip.SetContentMarginAll(8);
        tip.ShadowColor = new Color(0, 0, 0, 0.35f);
        tip.ShadowSize = 4;
        t.SetStylebox("panel", "TooltipPanel", tip);
        t.SetColor("font_color", "TooltipLabel", Ink);
        t.SetConstant("outline_size", "TooltipLabel", 0);
        t.SetFontSize("font_size", "TooltipLabel", 14);

        // ukazatel postupu stavby
        var pbBg = new StyleBoxFlat { BgColor = new Color(0.12f, 0.08f, 0.05f), BorderColor = new Color(0.45f, 0.32f, 0.16f) };
        pbBg.SetBorderWidthAll(1);
        pbBg.SetCornerRadiusAll(3);
        var pbFill = new StyleBoxFlat { BgColor = new Color(0.80f, 0.60f, 0.24f) };
        pbFill.SetCornerRadiusAll(3);
        t.SetStylebox("background", "ProgressBar", pbBg);
        t.SetStylebox("fill", "ProgressBar", pbFill);
        t.SetColor("font_color", "ProgressBar", Parchment);
        t.SetFontSize("font_size", "ProgressBar", 12);

        var sep = new StyleBoxLine { Color = new Color(0.62f, 0.46f, 0.22f, 0.7f), Thickness = 2, Vertical = true };
        t.SetStylebox("separator", "VSeparator", sep);
        t.SetConstant("separation", "VSeparator", 14);
        Theme = t;
    }

    static StyleBoxTexture Box(ImageTexture tex, int margin, int content)
    {
        var sb = new StyleBoxTexture
        {
            Texture = tex,
            AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.Tile,
            AxisStretchVertical = StyleBoxTexture.AxisStretchMode.Tile,
        };
        sb.SetTextureMarginAll(margin);
        sb.SetContentMarginAll(content);
        return sb;
    }

    /// <summary>Dřevěná deska: prkna s letokruhy, rám s plastickou hranou, volitelně zlatá linka a nýty.</summary>
    static ImageTexture WoodBox(int size, int margin, Color wood, bool frame, bool gold, bool rivets, bool inset, RandomNumberGenerator rng)
    {
        var c = new PixelCanvas(size, size);
        int period = size - 2 * margin;
        int plankH = Mathf.Max(4, period / 4);
        var n = new TileableNoise(rng, 2, 4, 8);
        var n2 = new TileableNoise(rng, 3, 6);
        var plankTint = new float[8];
        for (int i = 0; i < plankTint.Length; i++) plankTint[i] = rng.RandfRange(0.88f, 1.1f);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int lx = ((x - margin) % period + period) % period;
            int ly = ((y - margin) % period + period) % period;
            float u = (float)lx / period, v = (float)ly / period;
            int plank = ly / plankH;
            float grain = 1f + (n.Sample(u, v) - 0.5f) * 0.3f;
            float streak = 1f + Mathf.Sin((v * 12f + n2.Sample(u, v) * 2.5f) * Mathf.Tau) * 0.06f;
            float f = plankTint[plank % plankTint.Length] * grain * streak;
            if (ly % plankH == 0) f *= 0.62f;
            else if (ly % plankH == 1) f *= 1.12f;
            c.Set(x, y, Gfx.Shade(wood, f));
        }

        if (frame)
        {
            var fw = Gfx.Shade(wood, 0.75f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int dl = x, dt = y, dr = size - 1 - x, db = size - 1 - y;
                int d = Mathf.Min(Mathf.Min(dl, dt), Mathf.Min(dr, db));
                if (d >= margin) continue;
                bool lightSide = Mathf.Min(dl, dt) <= Mathf.Min(dr, db);
                if (inset) lightSide = !lightSide;
                var col = c.Get(x, y);
                if (d == 0) col = new Color(0.08f, 0.05f, 0.03f);
                else if (gold && d == margin - 3) col = new Color(0.86f, 0.68f, 0.32f) * (lightSide ? 1.12f : 0.82f);
                else if (gold && d == margin - 2) col = new Color(0.30f, 0.20f, 0.08f);
                else if (d == margin - 1) col = Gfx.Shade(col, inset ? 0.55f : 0.7f);
                else col = Gfx.Shade(Gfx.Shade(fw, lightSide ? 1.28f : 0.72f), 0.9f + 0.2f * (float)d / margin);
                col.A = 1f;
                c.Set(x, y, col);
            }
        }

        if (rivets)
        {
            float r = margin * 0.5f;
            foreach (var p in new[] { new Vector2(r, r), new Vector2(size - r, r), new Vector2(r, size - r), new Vector2(size - r, size - r) })
            {
                c.Disc(p + new Vector2(0.6f, 0.8f), 2.8f, 2.8f, new Color(0.05f, 0.03f, 0.02f), 0.6f);
                c.Disc(p, 2.6f, 2.6f, new Color(0.84f, 0.66f, 0.32f), 1f, 0.5f);
                c.Disc(p + new Vector2(-0.8f, -0.8f), 0.9f, 0.9f, new Color(1f, 0.93f, 0.7f), 0.9f);
            }
        }
        return c.ToTexture(false);
    }
}
