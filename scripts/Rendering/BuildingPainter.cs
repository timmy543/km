using System.Collections.Generic;
using Godot;

namespace km;

/// <summary>
/// Vektorové kreslení budov ve stylu Knights and Merchants: fasáda čelem k divákovi (jih),
/// vchod dole, střecha je hlavní viditelná plocha. Materiály zdí (hrázděné, kamenné, roubené)
/// a střech (tašky, došky, břidlice), dva tvary střechy, fáze stavby a průhledný "duch".
/// Rohy půdorysu v lokálních souřadnicích: nw, ne (vzadu), se, sw (vpředu u fasády).
/// </summary>
public static class BuildingPainter
{
    const float T = Iso.Tile;
    static readonly Color Beam = new(0.26f, 0.17f, 0.09f);
    static readonly Color Foundation = new(0.56f, 0.53f, 0.48f);
    static readonly Color DugEarth = new(0.48f, 0.37f, 0.23f);
    static readonly Color Scaffold = new(0.66f, 0.50f, 0.30f);
    static readonly Color Rope = new(0.95f, 0.90f, 0.72f);
    static readonly Color Glass = new(0.10f, 0.09f, 0.10f);
    static readonly Color DoorWood = new(0.36f, 0.22f, 0.12f);
    static readonly Color StoneCol = new(0.60f, 0.57f, 0.52f);

    public static void Draw(CanvasItem ci, BuildingDef def, Vector2 nw, Vector2 ne, Vector2 se, Vector2 sw, Building? b, float alpha, float time)
    {
        var state = b?.State ?? BuildState.Done;
        if (state == BuildState.Digging)
        {
            DrawSite(ci, nw, ne, se, sw, b!.DigProgress, alpha);
            return;
        }

        float p = state == BuildState.Done ? 1f : b!.BuildProgress;
        float wallP = state == BuildState.Done ? 1f : Mathf.Clamp((p - 0.1f) / 0.55f, 0f, 1f);
        float roofP = state == BuildState.Done ? 1f : Mathf.Clamp((p - 0.65f) / 0.35f, 0f, 1f);

        DrawGroundShadow(ci, nw, ne, se, sw, def.WallH * wallP + (roofP >= 1f ? def.RoofH * 0.8f : 0f), alpha);
        Poly(ci, Gfx.Alpha(Foundation, alpha), nw, ne, se, sw);
        Poly(ci, Gfx.Alpha(Gfx.Shade(Foundation, 0.68f), alpha), sw, se, se + new Vector2(0, 4), sw + new Vector2(0, 4));

        bool done = state == BuildState.Done;
        DrawHouse(ci, def, nw, ne, se, sw, def.WallH * wallP, roofP, alpha, done, b?.IsWorking ?? false, time);
        if (done)
        {
            if (b != null) DrawStock(ci, def, b, se, sw, alpha);
            return;
        }
        if (roofP < 1f) DrawScaffold(ci, sw, se, def.WallH + 6f, alpha);
        DrawMaterials(ci, b!, se, sw, alpha);
    }

    // ---------------------------------------------------------------- pomocné

    static void Poly(CanvasItem ci, Color c, params Vector2[] pts) => ci.DrawColoredPolygon(pts, c);

    static Vector2 Up(float h) => new(0, -h);

    static float Hash(int a, int b)
    {
        // míchání ve stylu murmur3 – bez pravidelných vzorů
        uint h = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA77u;
        h ^= h >> 15;
        h *= 0x2C1B3C6Du;
        h ^= h >> 12;
        h *= 0x297A2D39u;
        h ^= h >> 15;
        return (h & 0xFFFFFF) / (float)0xFFFFFF;
    }

    /// <summary>Čtyřúhelník se svislým přechodem: spodní hrana (a, b) tmavší, horní (c, d) světlejší.</summary>
    static void GradQuad(CanvasItem ci, Color col, float bottom, float top, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        var lo = Gfx.Shade(col, bottom);
        var hi = Gfx.Shade(col, top);
        ci.DrawPolygon(new[] { a, b, c, d }, new[] { lo, lo, hi, hi });
    }

    /// <summary>Bod na fasádě: x v pixelech od levého rohu, y v pixelech nad zemí.</summary>
    static Vector2 F(Vector2 sw, Vector2 se, float x, float y) => sw.Lerp(se, x / Mathf.Max(se.X - sw.X, 1f)) + Up(y);

    static void DrawGroundShadow(CanvasItem ci, Vector2 nw, Vector2 ne, Vector2 se, Vector2 sw, float h, float alpha)
    {
        if (h < 2f) return;
        var off = new Vector2(0.55f, 0.22f) * h;
        var hull = Geometry2D.ConvexHull(new[] { nw, ne, se, sw, nw + off, ne + off, se + off, sw + off });
        if (hull.Length > 1 && hull[0] == hull[^1]) hull = hull[..^1];
        if (hull.Length >= 3) ci.DrawColoredPolygon(hull, new Color(0.05f, 0.08f, 0.02f, 0.28f * alpha));
    }

    static void DrawSite(CanvasItem ci, Vector2 nw, Vector2 ne, Vector2 se, Vector2 sw, float dig, float alpha)
    {
        Poly(ci, Gfx.Alpha(DugEarth, alpha * (0.25f + 0.75f * dig)), nw, ne, se, sw);
        var stakes = new[] { nw, ne, se, sw };
        foreach (var p in stakes) ci.DrawLine(p, p + Up(12), Gfx.Alpha(Scaffold, alpha), 2.2f);
        var rope = new Vector2[5];
        for (int i = 0; i < 4; i++) rope[i] = stakes[i] + Up(10);
        rope[4] = rope[0];
        ci.DrawPolyline(rope, Gfx.Alpha(Rope, alpha * 0.9f), 1f);
    }

    // ---------------------------------------------------------------- dům

    static void DrawHouse(CanvasItem ci, BuildingDef def, Vector2 nw, Vector2 ne, Vector2 se, Vector2 sw,
        float wh, float roofP, float alpha, bool details, bool working, float time)
    {
        bool fullWalls = wh >= def.WallH - 0.01f;

        // u štítu čelem dozadu kreslíme nejdřív střechu (je za fasádou), jinak fasádu
        if (wh > 1f) DrawFacade(ci, def, sw, se, wh, alpha);
        if (fullWalls && details) DrawOpenings(ci, def, sw, se, wh, alpha);
        if (roofP <= 0f || !fullWalls) return;

        var flT = sw + Up(wh);
        var frT = se + Up(wh);
        var blT = nw + Up(wh);
        var brT = ne + Up(wh);
        float rh = def.RoofH;
        const float ex = 7f, drop = 5f;

        var planes = new List<(Vector2 eA, Vector2 eB, Vector2 rA, Vector2 rB, float shade)>();
        if (def.Shape == RoofShape.GableSide)
        {
            var rL = (blT + flT) * 0.5f + Up(rh);
            var rR = (brT + frT) * 0.5f + Up(rh);
            // zadní rovina (od hřebene dozadu) – jen úzký pruh nad hřebenem
            planes.Add((blT + new Vector2(-ex, 0), brT + new Vector2(ex, 0), rL + new Vector2(-ex, 0), rR + new Vector2(ex, 0), 0.62f));
            planes.Add((flT + new Vector2(-ex, drop), frT + new Vector2(ex, drop), rL + new Vector2(-ex, 0), rR + new Vector2(ex, 0), 1f));
        }
        else
        {
            var front = (flT + frT) * 0.5f + Up(rh);
            var back = (blT + brT) * 0.5f + Up(rh);
            planes.Add((flT + new Vector2(-ex, drop), blT + new Vector2(-ex, 0), front, back, 1.06f));
            planes.Add((frT + new Vector2(ex, drop), brT + new Vector2(ex, 0), front, back, 0.7f));
        }

        if (roofP < 1f)
        {
            DrawRafters(ci, planes, roofP, alpha);
            return;
        }

        var roof = Gfx.Alpha(def.Roof, alpha);
        for (int i = 0; i < planes.Count; i++)
        {
            var (eA, eB, rA, rB, shade) = planes[i];
            DrawRoofPlane(ci, def.RoofMat, Gfx.Shade(roof, shade), eA, eB, rA, rB, i);
        }

        var edge = Gfx.Alpha(def.RoofMat == RoofMaterial.Thatch ? Gfx.Shade(def.Roof, 0.6f) : Beam, alpha);
        if (def.Shape == RoofShape.GableSide)
        {
            var (eA, eB, rA, rB, _) = planes[1];
            ci.DrawLine(rA, rB, Gfx.Shade(roof, 0.5f), def.RoofMat == RoofMaterial.Thatch ? 5f : 3f);
            ci.DrawLine(eA, rA, edge, 2f);
            ci.DrawLine(eB, rB, edge, 2f);
            if (def.Chimney && details) DrawChimney(ci, eA.Lerp(eB, 0.78f).Lerp(rA.Lerp(rB, 0.78f), 0.5f), alpha, working, time);
        }
        else
        {
            var (lA, _, f, bk, _) = planes[0];
            var rAe = planes[1].eA;
            DrawGable(ci, def, flT, frT, f, alpha);
            ci.DrawLine(f, bk, Gfx.Shade(roof, 0.5f), def.RoofMat == RoofMaterial.Thatch ? 5f : 3f);
            // štítová prkna
            ci.DrawLine(lA, f, edge, 3.5f);
            ci.DrawLine(rAe, f, edge, 3.5f);
            if (def.Chimney && details) DrawChimney(ci, planes[1].eA.Lerp(planes[1].eB, 0.5f).Lerp(f.Lerp(bk, 0.5f), 0.4f), alpha, working, time);
        }
    }

    // ---------------------------------------------------------------- stěny

    static void DrawFacade(CanvasItem ci, BuildingDef def, Vector2 sw, Vector2 se, float wh, float alpha)
    {
        float width = se.X - sw.X;
        var wall = Gfx.Alpha(def.Wall, alpha);
        var beam = Gfx.Alpha(Beam, alpha);

        switch (def.Walls)
        {
            case WallStyle.Timber:
            {
                GradQuad(ci, wall, 0.84f, 1.04f, sw, se, se + Up(wh), sw + Up(wh));
                float ph = Mathf.Min(wh, 9f);
                StoneCourses(ci, sw, se, 0f, ph, Gfx.Alpha(StoneCol, alpha), 4.5f, 11f, 3);
                ci.DrawLine(F(sw, se, 0, ph), F(sw, se, width, ph), beam, 2.5f);
                if (wh > ph + 2f)
                {
                    float mid = Mathf.Min(wh, ph + (wh - ph) * 0.5f);
                    ci.DrawLine(F(sw, se, 0, mid), F(sw, se, width, mid), beam, 2f);
                    int sections = Mathf.RoundToInt(width / (T * 0.5f));
                    for (int k = 0; k <= sections; k++)
                    {
                        float x = width * k / sections;
                        ci.DrawLine(F(sw, se, x, ph), F(sw, se, x, wh), beam, k == 0 || k == sections ? 3f : 2f);
                        if (k < sections && wh > mid + 4f)
                        {
                            float x2 = width * (k + 1) / sections;
                            if (k % 2 == 0) ci.DrawLine(F(sw, se, x, mid), F(sw, se, x2, wh), beam, 1.6f);
                            else ci.DrawLine(F(sw, se, x2, mid), F(sw, se, x, wh), beam, 1.6f);
                        }
                    }
                }
                ci.DrawLine(F(sw, se, -2, wh), F(sw, se, width + 2, wh), beam, 3f);
                break;
            }
            case WallStyle.Stone:
                GradQuad(ci, wall, 0.82f, 1.02f, sw, se, se + Up(wh), sw + Up(wh));
                StoneCourses(ci, sw, se, 0f, wh, wall, 7f, 14f, 7);
                // nárožní kvádry
                for (float y = 0; y < wh - 1f; y += 7f)
                {
                    bool longL = ((int)(y / 7f)) % 2 == 0;
                    float y1 = Mathf.Min(y + 7f, wh);
                    Poly(ci, Gfx.Shade(wall, 1.12f), F(sw, se, 0, y), F(sw, se, longL ? 12 : 7, y), F(sw, se, longL ? 12 : 7, y1), F(sw, se, 0, y1));
                    Poly(ci, Gfx.Shade(wall, 1.12f), F(sw, se, width - (longL ? 7 : 12), y), F(sw, se, width, y), F(sw, se, width, y1), F(sw, se, width - (longL ? 7 : 12), y1));
                }
                break;
            case WallStyle.Logs:
            {
                const float logH = 6.5f;
                for (float y = 0; y < wh - 0.5f; y += logH)
                {
                    float y1 = Mathf.Min(y + logH, wh);
                    float s = 0.9f + Hash((int)y, 5) * 0.2f;
                    var col = Gfx.Shade(wall, s);
                    ci.DrawPolygon(new[] { F(sw, se, 0, y), F(sw, se, width, y), F(sw, se, width, y1), F(sw, se, 0, y1) },
                        new[] { Gfx.Shade(col, 0.62f), Gfx.Shade(col, 0.62f), Gfx.Shade(col, 1.15f), Gfx.Shade(col, 1.15f) });
                    ci.DrawLine(F(sw, se, 0, y), F(sw, se, width, y), Gfx.Shade(col, 0.4f), 1f);
                    // čela trámů přesahují roh
                    foreach (var x in new[] { -2.5f, width + 2.5f })
                    {
                        var c = F(sw, se, x, (y + y1) * 0.5f);
                        ci.DrawCircle(c, logH * 0.55f, Gfx.Shade(col, 0.5f));
                        ci.DrawCircle(c, logH * 0.42f, Gfx.Alpha(new Color(0.78f, 0.62f, 0.40f), alpha));
                    }
                }
                break;
            }
        }
        ci.DrawPolyline(new[] { sw, se, se + Up(wh), sw + Up(wh), sw }, Gfx.Alpha(new Color(0.12f, 0.08f, 0.05f), alpha * 0.8f), 1.2f);
    }

    /// <summary>Řádky kamenů s posunutými spárami a mírně odlišnými odstíny.</summary>
    static void StoneCourses(CanvasItem ci, Vector2 sw, Vector2 se, float y0, float y1, Color stone, float rowH, float blockW, int seed)
    {
        float width = se.X - sw.X;
        var joint = Gfx.Shade(stone, 0.55f);
        int row = 0;
        for (float y = y0; y < y1 - 0.5f; y += rowH, row++)
        {
            float ya = y, yb = Mathf.Min(y + rowH, y1);
            float off = row % 2 == 0 ? 0f : blockW * 0.5f;
            int col = 0;
            for (float x = -off; x < width; x += blockW, col++)
            {
                float xa = Mathf.Max(0f, x), xb = Mathf.Min(width, x + blockW);
                if (xb - xa < 0.5f) continue;
                float s = 0.86f + Hash(row * 31 + seed, col) * 0.24f;
                ci.DrawPolygon(new[] { F(sw, se, xa, ya), F(sw, se, xb, ya), F(sw, se, xb, yb), F(sw, se, xa, yb) },
                    new[] { Gfx.Shade(stone, s * 0.9f), Gfx.Shade(stone, s * 0.9f), Gfx.Shade(stone, s * 1.06f), Gfx.Shade(stone, s * 1.06f) });
                ci.DrawLine(F(sw, se, xa, ya), F(sw, se, xa, yb), joint, 1f);
            }
            ci.DrawLine(F(sw, se, 0, ya), F(sw, se, width, ya), joint, 1f);
        }
    }

    static void DrawGable(CanvasItem ci, BuildingDef def, Vector2 l, Vector2 r, Vector2 apex, float alpha)
    {
        var beam = Gfx.Alpha(Beam, alpha);
        switch (def.Walls)
        {
            case WallStyle.Timber:
                ci.DrawPolygon(new[] { l, r, apex }, new[] { Gfx.Alpha(def.Wall, alpha), Gfx.Alpha(def.Wall, alpha), Gfx.Alpha(Gfx.Shade(def.Wall, 1.05f), alpha) });
                ci.DrawLine((l + r) * 0.5f, apex, beam, 2f);
                ci.DrawLine(l.Lerp(apex, 0.5f), r.Lerp(apex, 0.5f), beam, 2f);
                break;
            default:
            {
                // prkenný štít
                var boards = Gfx.Alpha(new Color(0.46f, 0.31f, 0.18f), alpha);
                ci.DrawColoredPolygon(new[] { l, r, apex }, boards);
                int n = Mathf.RoundToInt((r.X - l.X) / 7f);
                for (int k = 1; k < n; k++)
                {
                    float x = l.X + (r.X - l.X) * k / n;
                    float t = 1f - Mathf.Abs(x - apex.X) / ((r.X - l.X) * 0.5f);
                    var bottom = l.Lerp(r, (float)k / n);
                    ci.DrawLine(bottom, new Vector2(x, bottom.Y + (apex.Y - bottom.Y) * t), Gfx.Shade(boards, 0.6f), 1f);
                }
                break;
            }
        }
        // větrací otvor
        var v = (l + r) * 0.5f + (apex - (l + r) * 0.5f) * 0.42f;
        ci.DrawRect(new Rect2(v + new Vector2(-4, -5), new Vector2(8, 9)), Gfx.Alpha(Glass, alpha));
        ci.DrawRect(new Rect2(v + new Vector2(-4, -5), new Vector2(8, 9)), beam, false, 1.4f);
        ci.DrawLine(l, r, beam, 2.5f);
    }

    static void DrawOpenings(CanvasItem ci, BuildingDef def, Vector2 sw, Vector2 se, float wh, float alpha)
    {
        var beam = Gfx.Alpha(Beam, alpha);
        var glass = Gfx.Alpha(Glass, alpha);
        float width = se.X - sw.X;

        // dveře
        float dc = (def.EntranceX + 0.5f) * T;
        float dw = def.WideDoor ? 0.4f * T : 0.2f * T;
        float dh = wh * (def.WideDoor ? 0.7f : 0.64f);
        var d0 = F(sw, se, dc - dw, 0);
        var d1 = F(sw, se, dc + dw, 0);
        var door = Gfx.Alpha(DoorWood, alpha);
        if (def.Walls == WallStyle.Stone)
        {
            // klenutý vstup
            var pts = new List<Vector2> { d0, d1 };
            for (int i = 0; i <= 8; i++)
            {
                float a = Mathf.Pi * i / 8f;
                pts.Add(F(sw, se, dc + Mathf.Cos(a) * dw, dh - dw + Mathf.Sin(a) * dw));
            }
            ci.DrawColoredPolygon(pts.ToArray(), door);
            pts.Add(d0);
            ci.DrawPolyline(pts.GetRange(1, pts.Count - 1).ToArray(), Gfx.Alpha(Gfx.Shade(def.Wall, 1.25f), alpha), 2.5f);
        }
        else
        {
            Poly(ci, door, d0, d1, F(sw, se, dc + dw, dh), F(sw, se, dc - dw, dh));
            ci.DrawPolyline(new[] { d0, F(sw, se, dc - dw, dh), F(sw, se, dc + dw, dh), d1 }, beam, 2.2f);
        }
        for (float x = dc - dw + 4f; x < dc + dw - 1f; x += 4f)
            ci.DrawLine(F(sw, se, x, 1), F(sw, se, x, dh - (def.Walls == WallStyle.Stone ? dw * 0.4f : 1f)), Gfx.Shade(door, 0.7f), 1f);
        ci.DrawLine(F(sw, se, dc - dw + 1, dh * 0.3f), F(sw, se, dc - dw + 6, dh * 0.3f), Gfx.Alpha(new Color(0.15f, 0.15f, 0.15f), alpha), 1.5f);
        ci.DrawLine(F(sw, se, dc - dw + 1, dh * 0.7f), F(sw, se, dc - dw + 6, dh * 0.7f), Gfx.Alpha(new Color(0.15f, 0.15f, 0.15f), alpha), 1.5f);

        // okna
        int tiles = def.Size.X;
        for (int k = 0; k < tiles; k++)
        {
            if (k == def.EntranceX || (def.WideDoor && Mathf.Abs(k - def.EntranceX) < 1)) continue;
            float xc = (k + 0.5f) * T;
            float ww = 0.15f * T;
            float y0 = wh * 0.42f, y1 = wh * 0.78f;
            if (def.Walls != WallStyle.Stone)
            {
                var sh = Gfx.Alpha(def.Walls == WallStyle.Logs ? new Color(0.40f, 0.26f, 0.14f) : new Color(0.24f, 0.36f, 0.22f), alpha);
                Poly(ci, sh, F(sw, se, xc - ww - 6, y0), F(sw, se, xc - ww, y0), F(sw, se, xc - ww, y1), F(sw, se, xc - ww - 6, y1));
                Poly(ci, sh, F(sw, se, xc + ww, y0), F(sw, se, xc + ww + 6, y0), F(sw, se, xc + ww + 6, y1), F(sw, se, xc + ww, y1));
            }
            Poly(ci, glass, F(sw, se, xc - ww, y0), F(sw, se, xc + ww, y0), F(sw, se, xc + ww, y1), F(sw, se, xc - ww, y1));
            // odlesk
            Poly(ci, Gfx.Alpha(new Color(0.55f, 0.62f, 0.7f, 0.35f), alpha), F(sw, se, xc - ww, y1), F(sw, se, xc - ww + 4, y1), F(sw, se, xc - ww + 9, y0 + 2), F(sw, se, xc - ww + 5, y0 + 2));
            var frame = def.Walls == WallStyle.Stone ? Gfx.Alpha(Gfx.Shade(def.Wall, 1.25f), alpha) : beam;
            ci.DrawPolyline(new[] { F(sw, se, xc - ww, y0), F(sw, se, xc + ww, y0), F(sw, se, xc + ww, y1), F(sw, se, xc - ww, y1), F(sw, se, xc - ww, y0) }, frame, 1.8f);
            ci.DrawLine(F(sw, se, xc, y0), F(sw, se, xc, y1), frame, 1.2f);
            ci.DrawLine(F(sw, se, xc - ww, (y0 + y1) * 0.5f), F(sw, se, xc + ww, (y0 + y1) * 0.5f), frame, 1.2f);
            if (def.Walls == WallStyle.Stone)
                ci.DrawLine(F(sw, se, xc - ww - 3, y1 + 2), F(sw, se, xc + ww + 3, y1 + 2), frame, 3f);
        }

        // vývěsní štít řemesla vedle dveří
        
        {
            var anchor = F(sw, se, dc + dw + 3f, dh * 0.95f);
            var tip = anchor + new Vector2(11, 0);
            ci.DrawLine(anchor, tip, beam, 2f);
            var board = new Rect2(tip + new Vector2(-7, 2), new Vector2(14, 11));
            ci.DrawLine(tip + new Vector2(-5, 0), board.Position + new Vector2(2, 0), beam, 1f);
            ci.DrawLine(tip + new Vector2(5, 0), board.Position + new Vector2(12, 0), beam, 1f);
            ci.DrawRect(board, Gfx.Alpha(new Color(0.55f, 0.38f, 0.2f), alpha));
            ci.DrawRect(board, beam, false, 1.2f);
            ci.DrawCircle(board.GetCenter(), 3.4f, Gfx.Alpha(def.Sign, alpha));
        }
    }

    // ---------------------------------------------------------------- střecha

    static Vector2 PlanePoint(Vector2 eA, Vector2 eB, Vector2 rA, Vector2 rB, float u, float t) =>
        eA.Lerp(eB, u).Lerp(rA.Lerp(rB, u), t);

    static void DrawRoofPlane(CanvasItem ci, RoofMaterial mat, Color roof, Vector2 eA, Vector2 eB, Vector2 rA, Vector2 rB, int seed)
    {
        float len = Mathf.Max(eA.DistanceTo(eB), rA.DistanceTo(rB));
        switch (mat)
        {
            case RoofMaterial.Tiles:
            case RoofMaterial.Slate:
            {
                bool slate = mat == RoofMaterial.Slate;
                int rows = slate ? 9 : 7;
                int cols = Mathf.Max(3, Mathf.RoundToInt(len / (slate ? 6f : 8f)));
                var joint = Gfx.Shade(roof, 0.55f);
                for (int r = 0; r < rows; r++)
                {
                    float t0 = (float)r / rows, t1 = (float)(r + 1) / rows;
                    float shade = Mathf.Lerp(0.9f, 1.1f, t0);
                    if (slate)
                    {
                        float off = r % 2 == 0 ? 0f : 0.5f;
                        for (int c = -1; c < cols; c++)
                        {
                            float u0 = Mathf.Max(0f, (c + off) / cols), u1 = Mathf.Min(1f, (c + 1 + off) / cols);
                            if (u1 <= u0) continue;
                            float s = shade * (0.86f + Hash(r * 17 + seed, c) * 0.26f);
                            ci.DrawPolygon(new[] { PlanePoint(eA, eB, rA, rB, u0, t0), PlanePoint(eA, eB, rA, rB, u1, t0), PlanePoint(eA, eB, rA, rB, u1, t1), PlanePoint(eA, eB, rA, rB, u0, t1) },
                                new[] { Gfx.Shade(roof, s * 0.85f), Gfx.Shade(roof, s * 0.85f), Gfx.Shade(roof, s), Gfx.Shade(roof, s) });
                        }
                    }
                    else
                    {
                        float s = shade * (r % 2 == 0 ? 0.96f : 1.04f);
                        ci.DrawPolygon(new[] { PlanePoint(eA, eB, rA, rB, 0, t0), PlanePoint(eA, eB, rA, rB, 1, t0), PlanePoint(eA, eB, rA, rB, 1, t1), PlanePoint(eA, eB, rA, rB, 0, t1) },
                            new[] { Gfx.Shade(roof, s * 0.82f), Gfx.Shade(roof, s * 0.82f), Gfx.Shade(roof, s), Gfx.Shade(roof, s) });
                    }
                    ci.DrawLine(PlanePoint(eA, eB, rA, rB, 0, t0), PlanePoint(eA, eB, rA, rB, 1, t0), joint, 1.2f);
                    float offset = r % 2 == 0 ? 0f : 0.5f;
                    for (int c = 0; c < cols; c++)
                    {
                        float u = (c + offset) / cols;
                        if (u <= 0f) continue;
                        ci.DrawLine(PlanePoint(eA, eB, rA, rB, u, t0), PlanePoint(eA, eB, rA, rB, u, t1), joint, 1f);
                    }
                }
                ci.DrawLine(eA, eB, Gfx.Shade(roof, 0.4f), 2f);
                break;
            }
            case RoofMaterial.Thatch:
            {
                ci.DrawPolygon(new[] { eA, eB, rB, rA }, new[] { Gfx.Shade(roof, 0.8f), Gfx.Shade(roof, 0.8f), Gfx.Shade(roof, 1.05f), Gfx.Shade(roof, 1.05f) });
                // vrstvy došků
                for (int band = 1; band < 4; band++)
                {
                    float t = band / 4f;
                    ci.DrawLine(PlanePoint(eA, eB, rA, rB, 0, t), PlanePoint(eA, eB, rA, rB, 1, t), Gfx.Shade(roof, 0.72f), 2f);
                }
                int streaks = Mathf.RoundToInt(len * 5f);
                for (int i = 0; i < streaks; i++)
                {
                    float u = Hash(i, seed * 7 + 1);
                    float t0 = Hash(i, seed * 7 + 2) * 0.85f;
                    float l = 0.05f + Hash(i, seed * 7 + 3) * 0.1f;
                    float s = 0.68f + Hash(i, seed * 7 + 4) * 0.6f;
                    ci.DrawLine(PlanePoint(eA, eB, rA, rB, u, t0), PlanePoint(eA, eB, rA, rB, u + (Hash(i, 9) - 0.5f) * 0.03f, Mathf.Min(1f, t0 + l)),
                        Gfx.Shade(roof, s), 1f);
                }
                // zaoblená okapová hrana
                ci.DrawLine(eA, eB, Gfx.Shade(roof, 0.55f), 5f);
                ci.DrawLine(eA + new Vector2(0, -2), eB + new Vector2(0, -2), Gfx.Shade(roof, 0.9f), 2f);
                break;
            }
        }
    }

    static void DrawRafters(CanvasItem ci, List<(Vector2 eA, Vector2 eB, Vector2 rA, Vector2 rB, float shade)> planes, float roofP, float alpha)
    {
        var sc = Gfx.Alpha(Scaffold, alpha);
        const int beams = 7;
        int shown = Mathf.CeilToInt(roofP * beams);
        foreach (var (eA, eB, rA, rB, shade) in planes)
        {
            for (int k = 0; k <= shown && k <= beams; k++)
            {
                float u = (float)k / beams;
                ci.DrawLine(PlanePoint(eA, eB, rA, rB, u, 0), PlanePoint(eA, eB, rA, rB, u, 1), Gfx.Shade(sc, shade), 2f);
            }
            ci.DrawLine(rA, rA.Lerp(rB, Mathf.Min(1f, (float)shown / beams)), sc, 2.5f);
        }
    }

    static void DrawChimney(CanvasItem ci, Vector2 c, float alpha, bool working, float time)
    {
        var brick = Gfx.Alpha(new Color(0.58f, 0.34f, 0.26f), alpha);
        GradQuad(ci, brick, 0.8f, 1.05f, c + new Vector2(-5, 4), c + new Vector2(5, 4), c + new Vector2(5, -18), c + new Vector2(-5, -18));
        ci.DrawRect(new Rect2(c + new Vector2(-6.5f, -21), new Vector2(13, 4)), Gfx.Shade(brick, 0.7f));
        for (int i = 0; i < 4; i++) ci.DrawLine(c + new Vector2(-5, -14 + i * 4.5f), c + new Vector2(5, -14 + i * 4.5f), Gfx.Shade(brick, 0.6f), 1f);
        if (!working) return;
        for (int k = 0; k < 4; k++)
        {
            float ph = (time * 0.55f + k * 0.25f) % 1f;
            var pos = c + new Vector2(ph * 12f + Mathf.Sin(time * 2f + k) * 2.5f, -24f - ph * 34f);
            ci.DrawCircle(pos, 3.5f + ph * 6f, new Color(0.88f, 0.88f, 0.86f, 0.5f * (1f - ph) * alpha));
        }
    }

    static void DrawScaffold(CanvasItem ci, Vector2 sw, Vector2 se, float h, float alpha)
    {
        var sc = Gfx.Alpha(Scaffold, alpha);
        float width = se.X - sw.X;
        var off = new Vector2(0, 5);
        foreach (var x in new[] { -3f, width * 0.5f, width + 3f })
            ci.DrawLine(F(sw, se, x, 0) + off, F(sw, se, x, h) + off, sc, 2.2f);
        foreach (var y in new[] { h * 0.45f, h * 0.9f })
            ci.DrawLine(F(sw, se, -5, y) + off, F(sw, se, width + 5, y) + off, Gfx.Shade(sc, 0.85f), 3f);
        ci.DrawLine(F(sw, se, -3, 0) + off, F(sw, se, width * 0.5f, h * 0.9f) + off, Gfx.Shade(sc, 0.8f), 1.5f);
    }

    // ---------------------------------------------------------------- zásoby před budovou

    /// <summary>Volné místo před fasádou na opačné straně než dveře.</summary>
    static Vector2 PileSpot(BuildingDef def, Vector2 se, Vector2 sw, bool other = false)
    {
        bool doorLeft = def.EntranceX + 0.5f < def.Size.X / 2f;
        bool right = doorLeft != other;
        return right ? se + new Vector2(-0.32f * T, 0.38f * T) : sw + new Vector2(0.32f * T, 0.38f * T);
    }

    static void DrawMaterials(CanvasItem ci, Building b, Vector2 se, Vector2 sw, float alpha)
    {
        int wood = (int)ResourceType.Wood, stone = (int)ResourceType.Stone;
        int usedWood = Mathf.Min(b.StepsDone, b.Def.CostWood);
        int usedStone = Mathf.Max(0, b.StepsDone - b.Def.CostWood);
        Planks(ci, sw + new Vector2(0.2f * T, 0.38f * T), b.Delivered[wood] - usedWood, alpha);
        StonePile(ci, se + new Vector2(-0.3f * T, 0.42f * T), b.Delivered[stone] - usedStone, alpha);
    }

    static void DrawStock(CanvasItem ci, BuildingDef def, Building b, Vector2 se, Vector2 sw, float alpha)
    {
        int trunk = b.Stock[(int)ResourceType.Trunk];
        int wood = b.Stock[(int)ResourceType.Wood];
        int stone = b.Stock[(int)ResourceType.Stone];
        if (def.IsStore)
        {
            var l = sw + new Vector2(0.3f * T, 0.36f * T);
            Crate(ci, l, alpha);
            Crate(ci, l + new Vector2(11, 3), alpha);
            Barrel(ci, se + new Vector2(-0.3f * T, 0.4f * T), alpha);
            Barrel(ci, se + new Vector2(-0.3f * T + 11, 0.4f * T + 2), alpha);
            return;
        }
        if (def.Inputs.Length > 0)
        {
            Logs(ci, PileSpot(def, se, sw, other: false), trunk, alpha);
            Planks(ci, PileSpot(def, se, sw, other: false) + new Vector2(0, 12), wood, alpha);
            return;
        }
        var spot = PileSpot(def, se, sw);
        if (trunk > 0) Logs(ci, spot, trunk, alpha);
        if (stone > 0) StonePile(ci, spot, stone, alpha);
    }

    static void Logs(CanvasItem ci, Vector2 p, int count, float alpha)
    {
        for (int i = 0; i < Mathf.Min(count, 6); i++)
        {
            int row = i < 3 ? 0 : i < 5 ? 1 : 2;
            int col = i < 3 ? i : i < 5 ? i - 3 : 0;
            var q = p + new Vector2(-8 + col * 6.5f + row * 3.2f, -row * 5f);
            ci.DrawLine(q, q + new Vector2(0, -1) + new Vector2(14, 0), Gfx.Alpha(Beam, alpha), 6f);
            ci.DrawLine(q, q + new Vector2(14, -1), Gfx.Alpha(Res.Tint(ResourceType.Trunk), alpha), 4.5f);
            ci.DrawCircle(q, 3f, Gfx.Alpha(Beam, alpha));
            ci.DrawCircle(q, 2.4f, Gfx.Alpha(new Color(0.84f, 0.68f, 0.44f), alpha));
        }
    }

    static void Planks(CanvasItem ci, Vector2 p, int count, float alpha)
    {
        for (int i = 0; i < Mathf.Min(count, 8); i++)
        {
            var q = p + new Vector2(-8 + (i % 2) * 2f, -i * 2.4f);
            ci.DrawLine(q, q + new Vector2(17, 0), Gfx.Alpha(Beam, alpha), 3.6f);
            ci.DrawLine(q, q + new Vector2(17, 0), Gfx.Alpha(Res.Tint(ResourceType.Wood), alpha), 2.4f);
        }
    }

    static void StonePile(CanvasItem ci, Vector2 p, int count, float alpha)
    {
        var offs = new[] { new Vector2(0, 0), new Vector2(7, 2), new Vector2(-6, 3), new Vector2(3, -4), new Vector2(10, -3), new Vector2(-3, -7), new Vector2(5, -9) };
        for (int i = 0; i < Mathf.Min(count, offs.Length); i++)
        {
            var q = p + offs[i];
            Gfx.Ellipse(ci, q, 4.4f, 3.4f, Gfx.Alpha(new Color(0.28f, 0.28f, 0.28f), alpha));
            Gfx.Ellipse(ci, q + new Vector2(-0.3f, -0.4f), 3.8f, 2.8f, Gfx.Alpha(Res.Tint(ResourceType.Stone), alpha));
            ci.DrawCircle(q + new Vector2(-1.2f, -1.2f), 1.2f, Gfx.Alpha(new Color(0.84f, 0.84f, 0.82f), alpha));
        }
    }

    static void Crate(CanvasItem ci, Vector2 p, float alpha)
    {
        var col = Gfx.Alpha(new Color(0.62f, 0.45f, 0.26f), alpha);
        ci.DrawRect(new Rect2(p + new Vector2(-5, -10), new Vector2(10, 10)), col);
        ci.DrawRect(new Rect2(p + new Vector2(-5, -13), new Vector2(10, 3)), Gfx.Shade(col, 1.2f));
        ci.DrawRect(new Rect2(p + new Vector2(-5, -13), new Vector2(10, 13)), Gfx.Alpha(Beam, alpha), false, 1f);
        ci.DrawLine(p + new Vector2(-5, -10), p + new Vector2(5, 0), Gfx.Alpha(Beam, alpha), 1f);
    }

    static void Barrel(CanvasItem ci, Vector2 p, float alpha)
    {
        var col = Gfx.Alpha(new Color(0.52f, 0.34f, 0.18f), alpha);
        ci.DrawRect(new Rect2(p + new Vector2(-4.5f, -11), new Vector2(9, 11)), col);
        Gfx.Ellipse(ci, p + new Vector2(0, -11), 4.5f, 2f, Gfx.Shade(col, 1.25f));
        ci.DrawLine(p + new Vector2(-4.5f, -3), p + new Vector2(4.5f, -3), Gfx.Alpha(new Color(0.15f, 0.15f, 0.15f), alpha), 1f);
        ci.DrawLine(p + new Vector2(-4.5f, -8), p + new Vector2(4.5f, -8), Gfx.Alpha(new Color(0.15f, 0.15f, 0.15f), alpha), 1f);
    }
}
