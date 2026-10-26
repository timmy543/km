using Godot;

namespace km;

/// <summary>
/// Vektorové kreslení hrázděných domů v izometrii: dvě viditelné stěny, sedlová střecha,
/// dveře, okna, znak řemesla na štítu. Umí i fáze stavby a průhledný "duch" při umisťování.
/// Rohy půdorysu: n = horní, e = pravý, s = spodní, w = levý (v lokálních souřadnicích).
/// </summary>
public static class BuildingPainter
{
    static readonly Color Frame = new(0.28f, 0.18f, 0.10f);
    static readonly Color Foundation = new(0.58f, 0.55f, 0.49f);
    static readonly Color DugEarth = new(0.50f, 0.38f, 0.24f);
    static readonly Color Scaffold = new(0.66f, 0.50f, 0.30f);
    static readonly Color Rope = new(0.95f, 0.90f, 0.72f);
    static readonly Color Dark = new(0.14f, 0.11f, 0.09f);

    public static void Draw(CanvasItem ci, BuildingDef def, Vector2 n, Vector2 e, Vector2 s, Vector2 w, Building? b, float alpha, float time)
    {
        var state = b?.State ?? BuildState.Done;
        if (state == BuildState.Digging)
        {
            DrawSite(ci, n, e, s, w, b!.DigProgress, alpha);
            return;
        }

        float shadowH = state == BuildState.Done
            ? def.WallH + def.RoofH * 0.7f
            : def.WallH * Mathf.Clamp((b!.BuildProgress - 0.1f) / 0.55f, 0f, 1f) * 0.8f;
        DrawGroundShadow(ci, n, e, s, w, shadowH, alpha);

        Poly(ci, Gfx.Alpha(Foundation, alpha), n, e, s, w);
        Poly(ci, Gfx.Alpha(Gfx.Shade(Foundation, 0.75f), alpha), w, s, s + new Vector2(0, 3), w + new Vector2(0, 3));
        Poly(ci, Gfx.Alpha(Gfx.Shade(Foundation, 0.6f), alpha), s, e, e + new Vector2(0, 3), s + new Vector2(0, 3));

        if (state == BuildState.Done)
        {
            DrawHouse(ci, def, n, e, s, w, def.WallH, 1f, alpha, true, b?.IsWorking ?? false, time);
            if (b != null) DrawStock(ci, def, b, s, e, w, alpha);
            return;
        }

        float p = b!.BuildProgress;
        float wallP = Mathf.Clamp((p - 0.1f) / 0.55f, 0f, 1f);
        float roofP = Mathf.Clamp((p - 0.65f) / 0.35f, 0f, 1f);
        DrawHouse(ci, def, n, e, s, w, def.WallH * wallP, roofP, alpha, false, false, time);
        if (roofP < 1f) DrawScaffold(ci, n, e, s, w, def.WallH + 4f, alpha);
        DrawMaterials(ci, b, w, s, alpha);
    }

    static void Poly(CanvasItem ci, Color c, params Vector2[] pts) => ci.DrawColoredPolygon(pts, c);

    /// <summary>Čtyřúhelník se svislým přechodem: spodní hrana (a, b) tmavší, horní (c, d) světlejší.</summary>
    static void GradQuad(CanvasItem ci, Color col, float bottom, float top, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        var lo = Gfx.Shade(col, bottom);
        var hi = Gfx.Shade(col, top);
        ci.DrawPolygon(new[] { a, b, c, d }, new[] { lo, lo, hi, hi });
    }

    /// <summary>Stín vržený doprava dolů (světlo zleva shora) – obal půdorysu a posunutého půdorysu.</summary>
    static void DrawGroundShadow(CanvasItem ci, Vector2 n, Vector2 e, Vector2 s, Vector2 w, float h, float alpha)
    {
        if (h < 2f) return;
        var off = new Vector2(0.8f, 0.42f) * h;
        var hull = Geometry2D.ConvexHull(new[] { n, e, s, w, n + off, e + off, s + off, w + off });
        if (hull.Length > 1 && hull[0] == hull[^1]) hull = hull[..^1];
        if (hull.Length >= 3) ci.DrawColoredPolygon(hull, new Color(0.05f, 0.08f, 0.02f, 0.26f * alpha));
    }

    static Vector2 Up(float h) => new(0, -h);

    static void DrawSite(CanvasItem ci, Vector2 n, Vector2 e, Vector2 s, Vector2 w, float dig, float alpha)
    {
        Poly(ci, Gfx.Alpha(DugEarth, alpha * (0.25f + 0.75f * dig)), n, e, s, w);
        var stakes = new[] { n, e, s, w };
        foreach (var p in stakes)
        {
            ci.DrawLine(p, p + Up(10), Gfx.Alpha(Scaffold, alpha), 2f);
        }
        var rope = new Vector2[5];
        for (int i = 0; i < 4; i++) rope[i] = stakes[i] + Up(8);
        rope[4] = rope[0];
        ci.DrawPolyline(rope, Gfx.Alpha(Rope, alpha * 0.9f), 1f);
    }

    static void DrawHouse(CanvasItem ci, BuildingDef def, Vector2 n, Vector2 e, Vector2 s, Vector2 w,
        float wh, float roofP, float alpha, bool details, bool working, float time)
    {
        var fr = Gfx.Alpha(Frame, alpha);
        Vector2 n2 = n + Up(wh), e2 = e + Up(wh), s2 = s + Up(wh), w2 = w + Up(wh);

        // --- stěny
        if (wh > 1f)
        {
            var wall = Gfx.Alpha(def.Wall, alpha);
            GradQuad(ci, wall, 0.82f, 1.04f, w, s, s2, w2);
            GradQuad(ci, wall, 0.6f, 0.8f, s, e, e2, s2);

            // hrázděné trámy
            ci.DrawLine(w, w2, fr, 2f);
            ci.DrawLine(s, s2, fr, 2.4f);
            ci.DrawLine(e, e2, fr, 2f);
            ci.DrawLine(w, s, fr, 1.6f);
            ci.DrawLine(s, e, fr, 1.6f);
            var mid = Up(wh * 0.45f);
            ci.DrawLine(w + mid, s + mid, fr, 1.4f);
            ci.DrawLine(s + mid, e + mid, fr, 1.4f);
            FaceFrame(ci, w, s, def.Size.X, wh, fr);
            FaceFrame(ci, s, e, def.Size.Y, wh, fr);
        }
        if (wh >= def.WallH - 0.01f)
        {
            ci.DrawLine(w2, s2, fr, 2f);
            ci.DrawLine(s2, e2, fr, 2f);
            if (details) DrawOpenings(ci, def, w, s, e, wh, alpha);
        }

        // --- střecha
        if (roofP <= 0f) return;
        float rh = def.RoofH;
        var r1 = (n2 + w2) * 0.5f + Up(rh);
        var r2 = (e2 + s2) * 0.5f + Up(rh);
        if (roofP < 1f)
        {
            // krovy
            int beams = def.Size.X * 3;
            int shown = Mathf.CeilToInt(roofP * beams);
            var sc = Gfx.Alpha(Scaffold, alpha);
            for (int k = 0; k <= shown && k <= beams; k++)
            {
                float t = (float)k / beams;
                ci.DrawLine(w2.Lerp(s2, t), r1.Lerp(r2, t), sc, 2f);
                ci.DrawLine(n2.Lerp(e2, t), r1.Lerp(r2, t), Gfx.Shade(sc, 0.8f), 1.5f);
            }
            ci.DrawLine(r1, r1.Lerp(r2, Mathf.Min(1f, (float)shown / beams)), sc, 2.5f);
            return;
        }

        var d = (r2 - r1).Normalized() * 5f;
        Vector2 Eave(Vector2 corner, Vector2 ridge) => corner + (corner - ridge) * 0.14f;
        var fw = Eave(w2, r1) - d;
        var fs = Eave(s2, r2) + d;
        var bn = Eave(n2, r1) - d;
        var be = Eave(e2, r2) + d;
        var r1x = r1 - d;
        var r2x = r2 + d;

        var roof = Gfx.Alpha(def.Roof, alpha);
        Poly(ci, Gfx.Shade(roof, 0.65f), bn, be, r2x, r1x);
        Poly(ci, Gfx.Alpha(Gfx.Shade(def.Wall, 0.84f), alpha), s2, e2, r2);
        ci.DrawLine(s2, r2, fr, 1.6f);
        ci.DrawLine(e2, r2, fr, 1.6f);
        ci.DrawLine((s2 + e2) * 0.5f, r2, fr, 1.4f);

        if (details)
        {
            // znak řemesla na štítu
            var em = (s2 + e2) * 0.5f + (r2 - (s2 + e2) * 0.5f) * 0.42f;
            ci.DrawCircle(em, 6.2f, fr);
            ci.DrawCircle(em, 5f, Gfx.Alpha(def.Sign, alpha));
        }

        DrawShingles(ci, roof, fw, fs, r1x, r2x);
        var edge = Gfx.Shade(roof, 0.5f);
        ci.DrawLine(fw, fs, edge, 2f);
        ci.DrawLine(r2x, fs, edge, 2f);
        ci.DrawLine(r2x, be, edge, 2f);
        ci.DrawLine(r1x, r2x, Gfx.Shade(roof, 0.4f), 3f);

        if (def.Chimney && details) DrawChimney(ci, fw, fs, r1x, r2x, alpha, working, time);
    }

    /// <summary>Přední rovina střechy po řadách tašek: střídavý odstín, spáry posunuté o půl tašky.</summary>
    static void DrawShingles(CanvasItem ci, Color roof, Vector2 eaveA, Vector2 eaveB, Vector2 ridgeA, Vector2 ridgeB)
    {
        const int rows = 7;
        int cols = Mathf.Max(4, Mathf.RoundToInt(eaveA.DistanceTo(eaveB) / 7f));
        var joint = Gfx.Shade(roof, 0.62f);
        for (int r = 0; r < rows; r++)
        {
            float t0 = (float)r / rows, t1 = (float)(r + 1) / rows;
            Vector2 a0 = eaveA.Lerp(ridgeA, t0), b0 = eaveB.Lerp(ridgeB, t0);
            Vector2 a1 = eaveA.Lerp(ridgeA, t1), b1 = eaveB.Lerp(ridgeB, t1);
            // řady u hřebene světlejší (víc na slunci), spodní hrana řady tmavá
            float shade = (r % 2 == 0 ? 0.96f : 1.03f) * Mathf.Lerp(0.92f, 1.1f, t0);
            ci.DrawPolygon(new[] { a0, b0, b1, a1 },
                new[] { Gfx.Shade(roof, shade * 0.86f), Gfx.Shade(roof, shade * 0.86f), Gfx.Shade(roof, shade), Gfx.Shade(roof, shade) });
            ci.DrawLine(a0, b0, joint, 1.2f);
            float offset = r % 2 == 0 ? 0f : 0.5f;
            for (int c = 0; c < cols; c++)
            {
                float u = (c + offset) / cols;
                if (u <= 0f) continue;
                ci.DrawLine(a0.Lerp(b0, u), a1.Lerp(b1, u), joint, 1f);
            }
        }
    }

    static void FaceFrame(CanvasItem ci, Vector2 a, Vector2 b, int tiles, float wh, Color fr)
    {
        for (int k = 0; k < tiles; k++)
        {
            var p0 = a.Lerp(b, (float)k / tiles);
            var p1 = a.Lerp(b, (float)(k + 1) / tiles);
            if (k > 0) ci.DrawLine(p0, p0 + Up(wh), fr, 1.6f);
            // šikmé vzpěry v horní polovině
            if (k % 2 == 0) ci.DrawLine(p0 + Up(wh * 0.45f), p1.Lerp(p0, 0.5f) + Up(wh), fr, 1.2f);
            else ci.DrawLine(p1 + Up(wh * 0.45f), p0.Lerp(p1, 0.5f) + Up(wh), fr, 1.2f);
        }
    }

    static void DrawOpenings(CanvasItem ci, BuildingDef def, Vector2 w, Vector2 s, Vector2 e, float wh, float alpha)
    {
        var dark = Gfx.Alpha(Dark, alpha);
        var fr = Gfx.Alpha(Frame, alpha);

        // dveře na levé stěně u vchodu
        var along = (s - w) / def.Size.X;
        var p = w + along * (def.EntranceX + 0.5f);
        float dw = def.WideDoor ? 0.32f : 0.2f;
        float dh = def.WideDoor ? 0.72f : 0.62f;
        var d0 = p - along * dw;
        var d1 = p + along * dw;
        Poly(ci, Gfx.Alpha(new Color(0.38f, 0.24f, 0.13f), alpha), d0, d1, d1 + Up(wh * dh), d0 + Up(wh * dh));
        ci.DrawPolyline(new[] { d0, d0 + Up(wh * dh), d1 + Up(wh * dh), d1 }, fr, 1.6f);
        ci.DrawLine(p, p + Up(wh * dh), Gfx.Alpha(new Color(0.25f, 0.15f, 0.08f), alpha), 1f);

        // okna
        for (int k = 0; k < def.Size.X; k++)
        {
            if (k == def.EntranceX) continue;
            Window(ci, w + along * (k + 0.5f), along, wh, dark, fr);
        }
        var alongR = (e - s) / def.Size.Y;
        for (int k = 0; k < def.Size.Y; k++)
            Window(ci, s + alongR * (k + 0.5f), alongR, wh, Gfx.Shade(dark, 0.8f), fr);
    }

    static void Window(CanvasItem ci, Vector2 p, Vector2 along, float wh, Color dark, Color fr)
    {
        var a = p - along * 0.15f + Up(wh * 0.5f);
        var b = p + along * 0.15f + Up(wh * 0.5f);
        var c = b + Up(wh * 0.28f);
        var d = a + Up(wh * 0.28f);
        Poly(ci, dark, a, b, c, d);
        ci.DrawPolyline(new[] { a, b, c, d, a }, fr, 1.4f);
        ci.DrawLine((a + b) * 0.5f, (c + d) * 0.5f, fr, 1f);
    }

    static void DrawChimney(CanvasItem ci, Vector2 fw, Vector2 fs, Vector2 r1, Vector2 r2, float alpha, bool working, float time)
    {
        var c = fs.Lerp(r2, 0.55f).Lerp(fw.Lerp(r1, 0.55f), 0.3f);
        var brick = Gfx.Alpha(new Color(0.58f, 0.34f, 0.26f), alpha);
        Poly(ci, brick, c + new Vector2(-4, 3), c + new Vector2(1, 3), c + new Vector2(1, -15), c + new Vector2(-4, -15));
        Poly(ci, Gfx.Shade(brick, 0.75f), c + new Vector2(1, 3), c + new Vector2(5, 1), c + new Vector2(5, -17), c + new Vector2(1, -15));
        Poly(ci, Gfx.Shade(brick, 0.5f), c + new Vector2(-4, -15), c + new Vector2(1, -15), c + new Vector2(5, -17), c + new Vector2(0, -17));
        if (!working) return;
        for (int k = 0; k < 4; k++)
        {
            float ph = (time * 0.55f + k * 0.25f) % 1f;
            var pos = c + new Vector2(0.5f + ph * 10f + Mathf.Sin(time * 2f + k) * 2.5f, -19f - ph * 30f);
            ci.DrawCircle(pos, 3f + ph * 5f, new Color(0.88f, 0.88f, 0.86f, 0.5f * (1f - ph) * alpha));
        }
    }

    static void DrawScaffold(CanvasItem ci, Vector2 n, Vector2 e, Vector2 s, Vector2 w, float h, float alpha)
    {
        var sc = Gfx.Alpha(Scaffold, alpha);
        foreach (var p in new[] { w, s, e })
            ci.DrawLine(p + new Vector2(0, 2), p + Up(h), sc, 2f);
        ci.DrawLine(w + Up(h * 0.5f), s + Up(h * 0.5f), sc, 1.5f);
        ci.DrawLine(s + Up(h * 0.5f), e + Up(h * 0.5f), sc, 1.5f);
        ci.DrawLine(w + Up(h), s + Up(h), sc, 1.5f);
        ci.DrawLine(s + Up(h), e + Up(h), sc, 1.5f);
    }

    static void DrawMaterials(CanvasItem ci, Building b, Vector2 w, Vector2 s, float alpha)
    {
        int wood = (int)ResourceType.Wood, stone = (int)ResourceType.Stone;
        int usedWood = Mathf.Min(b.StepsDone, b.Def.CostWood);
        int usedStone = Mathf.Max(0, b.StepsDone - b.Def.CostWood);
        var basePos = w.Lerp(s, 0.5f) + new Vector2(-6, 10);
        Planks(ci, basePos, b.Delivered[wood] - usedWood, alpha);
        StonePile(ci, basePos + new Vector2(22, 6), b.Delivered[stone] - usedStone, alpha);
    }

    static void DrawStock(CanvasItem ci, BuildingDef def, Building b, Vector2 s, Vector2 e, Vector2 w, float alpha)
    {
        var pos = s.Lerp(e, 0.55f) + new Vector2(12, 8);
        int trunk = b.Stock[(int)ResourceType.Trunk];
        int wood = b.Stock[(int)ResourceType.Wood];
        int stone = b.Stock[(int)ResourceType.Stone];
        if (def.IsStore)
        {
            // pár beden a sudů u vchodu jako dekorace
            var c = w.Lerp(s, 0.12f) + new Vector2(-4, 8);
            Crate(ci, c, alpha);
            Crate(ci, c + new Vector2(10, 3), alpha);
            Barrel(ci, s.Lerp(e, 0.3f) + new Vector2(8, 6), alpha);
            return;
        }
        if (trunk > 0) Logs(ci, def.Inputs.Length > 0 ? w.Lerp(s, 0.15f) + new Vector2(-8, 10) : pos, trunk, alpha);
        if (wood > 0) Planks(ci, pos, wood, alpha);
        if (stone > 0) StonePile(ci, pos, stone, alpha);
    }

    static void Logs(CanvasItem ci, Vector2 p, int count, float alpha)
    {
        for (int i = 0; i < Mathf.Min(count, 6); i++)
        {
            int row = i < 3 ? 0 : i < 5 ? 1 : 2;
            int col = i < 3 ? i : i < 5 ? i - 3 : 0;
            var q = p + new Vector2(col * 6f + row * 3f, -row * 4.5f);
            ci.DrawLine(q, q + new Vector2(12, 6), Gfx.Alpha(Frame, alpha), 5.5f);
            ci.DrawLine(q, q + new Vector2(12, 6), Gfx.Alpha(Res.Tint(ResourceType.Trunk), alpha), 4f);
            ci.DrawCircle(q, 2.6f, Gfx.Alpha(new Color(0.82f, 0.66f, 0.42f), alpha));
        }
    }

    static void Planks(CanvasItem ci, Vector2 p, int count, float alpha)
    {
        for (int i = 0; i < Mathf.Min(count, 8); i++)
        {
            var q = p + new Vector2((i % 2) * 2f, -i * 2.2f);
            ci.DrawLine(q, q + new Vector2(14, 7), Gfx.Alpha(Frame, alpha), 3.4f);
            ci.DrawLine(q, q + new Vector2(14, 7), Gfx.Alpha(Res.Tint(ResourceType.Wood), alpha), 2.2f);
        }
    }

    static void StonePile(CanvasItem ci, Vector2 p, int count, float alpha)
    {
        var offs = new[] { new Vector2(0, 0), new Vector2(7, 2), new Vector2(-6, 3), new Vector2(3, -4), new Vector2(10, -3), new Vector2(-3, -7), new Vector2(5, -9) };
        for (int i = 0; i < Mathf.Min(count, offs.Length); i++)
        {
            var q = p + offs[i];
            Gfx.Ellipse(ci, q, 4.2f, 3.2f, Gfx.Alpha(new Color(0.3f, 0.3f, 0.3f), alpha));
            Gfx.Ellipse(ci, q + new Vector2(-0.3f, -0.4f), 3.6f, 2.6f, Gfx.Alpha(Res.Tint(ResourceType.Stone), alpha));
            ci.DrawCircle(q + new Vector2(-1.2f, -1.2f), 1.2f, Gfx.Alpha(new Color(0.82f, 0.82f, 0.8f), alpha));
        }
    }

    static void Crate(CanvasItem ci, Vector2 p, float alpha)
    {
        var col = Gfx.Alpha(new Color(0.62f, 0.45f, 0.26f), alpha);
        Poly(ci, col, p, p + new Vector2(8, 4), p + new Vector2(8, -4), p + new Vector2(0, -8));
        Poly(ci, Gfx.Shade(col, 0.75f), p + new Vector2(8, 4), p + new Vector2(13, 1), p + new Vector2(13, -7), p + new Vector2(8, -4));
        Poly(ci, Gfx.Shade(col, 1.15f), p + new Vector2(0, -8), p + new Vector2(8, -4), p + new Vector2(13, -7), p + new Vector2(5, -11));
    }

    static void Barrel(CanvasItem ci, Vector2 p, float alpha)
    {
        var col = Gfx.Alpha(new Color(0.52f, 0.34f, 0.18f), alpha);
        ci.DrawRect(new Rect2(p + new Vector2(-4.5f, -11), new Vector2(9, 11)), col);
        Gfx.Ellipse(ci, p + new Vector2(0, -11), 4.5f, 2f, Gfx.Shade(col, 1.25f));
        ci.DrawLine(p + new Vector2(-4.5f, -3), p + new Vector2(4.5f, -3), Gfx.Alpha(Dark, alpha), 1f);
        ci.DrawLine(p + new Vector2(-4.5f, -8), p + new Vector2(4.5f, -8), Gfx.Alpha(Dark, alpha), 1f);
    }
}
