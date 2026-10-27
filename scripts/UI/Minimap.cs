using Godot;

namespace km;

/// <summary>
/// Minimapa ve stejné izometrii jako hra: textura má jeden pixel na dlaždici a kreslí se
/// transformovaná do kosočtverce. Ukazuje terén, lesy, skály, silnice, budovy, lidi
/// a výřez kamery. Kliknutím nebo tažením se kamera přesune.
/// </summary>
public partial class Minimap : Control
{
    Image img = null!;
    ImageTexture tex = null!;
    float timer;
    bool dragging;

    public Minimap()
    {
        CustomMinimumSize = new Vector2(152, 152);
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
    }

    public override void _Ready()
    {
        var map = Main.Instance.Map;
        img = Image.CreateEmpty(map.Width, map.Height, false, Image.Format.Rgba8);
        Repaint();
        tex = ImageTexture.CreateFromImage(img);
    }

    public override void _Process(double delta)
    {
        timer -= (float)delta;
        if (timer <= 0f)
        {
            timer = 0.5f;
            Repaint();
            tex.Update(img);
        }
        QueueRedraw();
    }

    void Repaint()
    {
        var map = Main.Instance.Map;
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
            img.SetPixel(x, y, Fogged(map, x, y, TileColor(map, x, y)));
    }

    static Color Fogged(GameMap map, int x, int y, Color c)
    {
        var e = map.Explored;
        float v = (e[x, y] + e[x + 1, y] + e[x, y + 1] + e[x + 1, y + 1]) * 0.25f;
        return Gfx.Shade(c, v);
    }

    static Color TileColor(GameMap map, int x, int y)
    {
        Color c = map.Ground[x, y] switch
        {
            Terrain.Grass => new Color(0.36f, 0.50f, 0.20f),
            Terrain.Dirt => new Color(0.50f, 0.41f, 0.26f),
            Terrain.Sand => new Color(0.80f, 0.72f, 0.50f),
            Terrain.Rock => new Color(0.55f, 0.53f, 0.49f),
            _ => new Color(0.16f, 0.32f, 0.45f).Lerp(new Color(0.08f, 0.2f, 0.33f),
                Mathf.Clamp((GameMap.WaterLevel - map.VertexElev[x, y]) * 2.5f, 0f, 1f)),
        };
        if (map.Ground[x, y] != Terrain.Water)
        {
            // jednoduché stínování kopců
            float slope = map.Heights[x + 1, y + 1] - map.Heights[x, y];
            c = Gfx.Shade(c, Mathf.Clamp(1f + slope * 0.04f, 0.7f, 1.3f));
        }

        if (map.Buildings[x, y] is { } b)
            c = b.State == BuildState.Done ? b.Def.Roof.Lerp(new Color(0.9f, 0.3f, 0.2f), 0.25f) : new Color(0.62f, 0.50f, 0.32f);
        else if (map.Roads[x, y]) c = new Color(0.72f, 0.63f, 0.48f);
        else if (map.RoadPlans[x, y] != null) c = c.Lerp(new Color(0.9f, 0.85f, 0.65f), 0.4f);
        else if (map.Stones[x, y] != null) c = new Color(0.78f, 0.77f, 0.74f);
        else if (map.Trees[x, y] is { } t) c = t.IsMature ? new Color(0.16f, 0.30f, 0.12f) : c.Lerp(new Color(0.16f, 0.30f, 0.12f), 0.5f);
        return c;
    }

    // ---------------------------------------------------------------- mapování svět ↔ minimapa

    float MiniScale
    {
        get
        {
            var map = Main.Instance.Map;
            float worldW = map.Width * Iso.Tile;
            float worldH = map.Height * Iso.Tile;
            return Mathf.Min(Size.X / worldW, Size.Y / worldH);
        }
    }

    Vector2 Origin
    {
        get
        {
            var map = Main.Instance.Map;
            var worldCenter = new Vector2(map.Width, map.Height) * Iso.Tile * 0.5f;
            return Size / 2f - worldCenter * MiniScale;
        }
    }

    Vector2 WorldToMini(Vector2 w) => Origin + w * MiniScale;

    public override void _Draw()
    {
        float k = MiniScale;
        var o = Origin;
        DrawSetTransformMatrix(new Transform2D(new Vector2(Iso.Tile * k, 0), new Vector2(0, Iso.Tile * k), o));
        DrawTexture(tex, Vector2.Zero);
        DrawSetTransformMatrix(Transform2D.Identity);

        // lidé
        foreach (var u in Main.Instance.Eco.AllUnits)
        {
            if (!u.Visible) continue;
            var p = WorldToMini(Iso.ToScreen(u.GridPos));
            DrawCircle(p, 1.6f, u.Carrying != null ? UiArt.Gold : UiArt.Parchment);
        }

        // výřez kamery
        var cam = Main.Instance.Cam;
        var half = GetViewportRect().Size / cam.Zoom / 2f;
        var a = WorldToMini(cam.Position - half);
        var b = WorldToMini(cam.Position + half);
        var rect = new Rect2(a, b - a);
        DrawRect(rect, new Color(0, 0, 0, 0.5f), false, 3f);
        DrawRect(rect, new Color(1f, 0.95f, 0.8f, 0.95f), false, 1.5f);
    }

    public override void _GuiInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } mb:
                dragging = mb.Pressed;
                if (mb.Pressed) JumpTo(mb.Position);
                AcceptEvent();
                break;
            case InputEventMouseMotion mm when dragging:
                JumpTo(mm.Position);
                AcceptEvent();
                break;
        }
    }

    void JumpTo(Vector2 local) => Main.Instance.Cam.JumpTo((local - Origin) / MiniScale);
}
