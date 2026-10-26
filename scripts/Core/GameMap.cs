using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace km;

public enum Terrain { Grass, Dirt, Sand, Rock, Water }

/// <summary>
/// Data mapy: terén, výšky ve vrcholech, obsazenost dlaždic a hledání cest (A* s váhami).
/// </summary>
public sealed class GameMap
{
    public const float WaterLevel = -0.28f;

    public readonly int Width, Height;
    public readonly float[,] Heights;          // (W+1)×(H+1) vrcholů, v pixelech
    public readonly Terrain[,] VertexGround;   // typ terénu ve vrcholu (pro barvy)
    public readonly float[,] VertexTint;       // jemná variace barvy 0..1
    public readonly float[,] VertexElev;       // surová nadmořská výška (šum) – pro hladký břeh
    public readonly Terrain[,] Ground;         // typ terénu dlaždice

    public readonly Building?[,] Buildings;
    public readonly Building?[,] EntranceOf;
    public readonly TreeObj?[,] Trees;
    public readonly StoneObj?[,] Stones;
    public readonly Sprite2D?[,] Decor;
    public readonly bool[,] Roads;
    public readonly RoadPlan?[,] RoadPlans;

    public readonly AStarGrid2D Astar = new();

    public readonly List<Vector2I> GenTrees = new();
    public readonly List<Vector2I> GenStones = new();
    public readonly List<Vector2I> GenDecor = new();

    public Vector2I Center => new(Width / 2, Height / 2);

    public GameMap(int width, int height)
    {
        Width = width;
        Height = height;
        Heights = new float[width + 1, height + 1];
        VertexGround = new Terrain[width + 1, height + 1];
        VertexTint = new float[width + 1, height + 1];
        VertexElev = new float[width + 1, height + 1];
        Ground = new Terrain[width, height];
        Buildings = new Building?[width, height];
        EntranceOf = new Building?[width, height];
        Trees = new TreeObj?[width, height];
        Stones = new StoneObj?[width, height];
        Decor = new Sprite2D?[width, height];
        Roads = new bool[width, height];
        RoadPlans = new RoadPlan?[width, height];
    }

    // ---------------------------------------------------------------- generování

    public void Generate(int seed)
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)seed };
        var hN = new FastNoiseLite
        {
            Seed = seed, NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Frequency = 0.03f,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm, FractalOctaves = 4,
        };
        var rockN = new FastNoiseLite { Seed = seed + 11, NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Frequency = 0.045f, FractalOctaves = 2 };
        var forestN = new FastNoiseLite { Seed = seed + 23, NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Frequency = 0.055f, FractalOctaves = 2 };
        var tintN = new FastNoiseLite { Seed = seed + 37, NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Frequency = 0.09f };
        var dirtN = new FastNoiseLite { Seed = seed + 51, NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Frequency = 0.07f };

        var c = new Vector2(Width / 2f, Height / 2f);

        float Elev(float x, float y)
        {
            float n = hN.GetNoise2D(x, y) * 1.3f;
            float d = new Vector2(x, y).DistanceTo(c);
            n += 0.6f * Mathf.Max(0f, 1f - d / 16f); // start je vždy na souši
            return n;
        }

        float Rocky(float x, float y, float n) => n > -0.05f ? rockN.GetNoise2D(x, y) : -1f;

        Terrain Classify(float x, float y)
        {
            float n = Elev(x, y);
            if (n < WaterLevel) return Terrain.Water;
            if (n < WaterLevel + 0.06f) return Terrain.Sand;
            if (Rocky(x, y, n) > 0.32f) return Terrain.Rock;
            if (dirtN.GetNoise2D(x, y) > 0.42f) return Terrain.Dirt;
            return Terrain.Grass;
        }

        for (int j = 0; j <= Height; j++)
        for (int i = 0; i <= Width; i++)
        {
            float n = Elev(i, j);
            float e = Mathf.Clamp((n - WaterLevel) / (1f - WaterLevel), 0f, 1f);
            float h = Mathf.Pow(e, 1.4f) * 42f;
            float r = Rocky(i, j, n);
            if (r > 0.2f) h += (r - 0.2f) * 70f;
            Heights[i, j] = h;
            VertexGround[i, j] = Classify(i, j);
            VertexTint[i, j] = tintN.GetNoise2D(i, j) * 0.5f + 0.5f;
            VertexElev[i, j] = n;
        }

        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            Ground[x, y] = Classify(x + 0.5f, y + 0.5f);

        // okolí startu: žádné skály
        ForEachInRadius(Center, 9f, t =>
        {
            if (Ground[t.X, t.Y] == Terrain.Rock) Ground[t.X, t.Y] = Terrain.Grass;
        });
        for (int j = 0; j <= Height; j++)
        for (int i = 0; i <= Width; i++)
            if (VertexGround[i, j] == Terrain.Rock && new Vector2(i, j).DistanceTo(c) < 10f)
                VertexGround[i, j] = Terrain.Grass;

        var trees = new HashSet<Vector2I>();
        var stones = new HashSet<Vector2I>();
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            var t = new Vector2I(x, y);
            float d = new Vector2(x, y).DistanceTo(c);
            var g = Ground[x, y];
            if ((g == Terrain.Grass || g == Terrain.Dirt) && d > 7f)
            {
                float f = forestN.GetNoise2D(x, y);
                float p = f > 0.2f ? 0.65f : f > 0.05f ? 0.2f : 0.025f;
                if (rng.Randf() < p) trees.Add(t);
            }
            else if (g == Terrain.Rock && d > 6f && rng.Randf() < 0.28f)
                stones.Add(t);
        }

        // zaručené zdroje poblíž startu
        if (trees.Count(t => Iso.TileCenter(t).DistanceTo(c) < 16f) < 30)
        {
            var fc = Center + new Vector2I(-11, 6);
            ForEachInRadius(fc, 4.5f, t =>
            {
                var g = Ground[t.X, t.Y];
                if ((g == Terrain.Grass || g == Terrain.Dirt) && rng.Randf() < 0.75f) trees.Add(t);
            });
        }
        if (stones.Count(t => Iso.TileCenter(t).DistanceTo(c) < 18f) < 4)
        {
            var sc = Center + new Vector2I(10, -9);
            ForEachInRadius(sc, 2.6f, t =>
            {
                if (Ground[t.X, t.Y] == Terrain.Water) return;
                Ground[t.X, t.Y] = Terrain.Rock;
                trees.Remove(t);
                if (rng.Randf() < 0.6f) stones.Add(t);
            });
            for (int j = sc.Y - 3; j <= sc.Y + 4; j++)
            for (int i = sc.X - 3; i <= sc.X + 4; i++)
                if (i >= 0 && j >= 0 && i <= Width && j <= Height && new Vector2(i, j).DistanceTo(Iso.TileCenter(sc)) < 3.2f)
                    VertexGround[i, j] = Terrain.Rock;
            stones.Add(sc);
        }

        GenTrees.AddRange(trees);
        GenStones.AddRange(stones);
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            var t = new Vector2I(x, y);
            if (Ground[x, y] == Terrain.Grass && !trees.Contains(t) && rng.Randf() < 0.14f)
                GenDecor.Add(t);
        }

        InitPathfinding();
    }

    void InitPathfinding()
    {
        Astar.Region = new Rect2I(0, 0, Width, Height);
        Astar.CellSize = Vector2.One;
        Astar.DiagonalMode = AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles;
        Astar.DefaultComputeHeuristic = AStarGrid2D.Heuristic.Octile;
        Astar.DefaultEstimateHeuristic = AStarGrid2D.Heuristic.Octile;
        Astar.Update();
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            RefreshTile(new Vector2I(x, y));
    }

    // ---------------------------------------------------------------- dotazy

    public bool InBounds(Vector2I t) => t.X >= 0 && t.Y >= 0 && t.X < Width && t.Y < Height;

    public bool IsWalkable(Vector2I t) =>
        InBounds(t) && Ground[t.X, t.Y] != Terrain.Water && Buildings[t.X, t.Y] == null && Stones[t.X, t.Y] == null;

    /// <summary>
    /// Cena vstupu na dlaždici pro A*. Silnice = 1 (minimum), aby heuristika zůstala přípustná.
    /// </summary>
    public float Weight(Vector2I t)
    {
        if (Roads[t.X, t.Y]) return 1f;
        float w = Ground[t.X, t.Y] switch
        {
            Terrain.Sand => 1.8f,
            Terrain.Rock => 2.0f,
            _ => 1.6f,
        };
        if (Trees[t.X, t.Y] != null) w += 1.2f;
        return w;
    }

    /// <summary>Násobek rychlosti chůze – odpovídá vahám v A*.</summary>
    public float SpeedFactor(Vector2I t)
    {
        if (!InBounds(t)) return 1f;
        if (Roads[t.X, t.Y]) return 1.55f;
        if (Trees[t.X, t.Y] != null) return 0.75f;
        return Ground[t.X, t.Y] is Terrain.Sand or Terrain.Rock ? 0.85f : 1f;
    }

    public void RefreshTile(Vector2I t)
    {
        bool solid = !IsWalkable(t);
        Astar.SetPointSolid(t, solid);
        Astar.SetPointWeightScale(t, Weight(t));
    }

    public float HeightAt(Vector2 g)
    {
        float gx = Mathf.Clamp(g.X, 0f, Width), gy = Mathf.Clamp(g.Y, 0f, Height);
        int i = Math.Min((int)gx, Width - 1), j = Math.Min((int)gy, Height - 1);
        float fx = gx - i, fy = gy - j;
        float top = Mathf.Lerp(Heights[i, j], Heights[i + 1, j], fx);
        float bot = Mathf.Lerp(Heights[i, j + 1], Heights[i + 1, j + 1], fx);
        return Mathf.Lerp(top, bot, fy);
    }

    public Vector2 GroundToScreen(Vector2 g) => Iso.ToScreen(g) - new Vector2(0, HeightAt(g));

    /// <summary>Inverze projekce i s výškou terénu (pár iterací stačí).</summary>
    public Vector2I ScreenToTile(Vector2 s)
    {
        var g = Iso.ToGrid(s);
        for (int k = 0; k < 4; k++)
            g = Iso.ToGrid(s + new Vector2(0, HeightAt(g)));
        return new Vector2I(Mathf.FloorToInt(g.X), Mathf.FloorToInt(g.Y));
    }

    public Vector2[] TileCorners(Vector2I t) => new[]
    {
        GroundToScreen(new Vector2(t.X, t.Y)),
        GroundToScreen(new Vector2(t.X + 1, t.Y)),
        GroundToScreen(new Vector2(t.X + 1, t.Y + 1)),
        GroundToScreen(new Vector2(t.X, t.Y + 1)),
    };

    public void ForEachInRadius(Vector2I c, float r, Action<Vector2I> fn)
    {
        int ri = Mathf.CeilToInt(r);
        for (int y = c.Y - ri; y <= c.Y + ri; y++)
        for (int x = c.X - ri; x <= c.X + ri; x++)
        {
            var t = new Vector2I(x, y);
            if (InBounds(t) && (t - c).LengthSquared() <= r * r) fn(t);
        }
    }

    // ---------------------------------------------------------------- hledání cest

    public List<Vector2I>? FindPath(Vector2I from, Vector2I to)
    {
        if (!InBounds(from) || !IsWalkable(to)) return null;
        var start = from;
        if (!IsWalkable(start))
        {
            var nw = NearestWalkable(start, 4);
            if (nw == null) return null;
            start = nw.Value;
        }
        if (start == to) return new List<Vector2I> { to };
        var path = Astar.GetIdPath(start, to);
        return path.Count == 0 ? null : new List<Vector2I>(path);
    }

    /// <summary>Cesta k nejbližšímu dosažitelnému z cílů (zkouší je podle vzdálenosti).</summary>
    public List<Vector2I>? FindPathToAny(Vector2I from, IReadOnlyList<Vector2I> targets)
    {
        foreach (var t in targets.Where(IsWalkable).OrderBy(t => (t - from).LengthSquared()).Take(4))
        {
            var p = FindPath(from, t);
            if (p != null) return p;
        }
        return null;
    }

    public Vector2I? NearestWalkable(Vector2I c, int maxR)
    {
        for (int r = 1; r <= maxR; r++)
        {
            Vector2I? best = null;
            int bd = int.MaxValue;
            for (int y = c.Y - r; y <= c.Y + r; y++)
            for (int x = c.X - r; x <= c.X + r; x++)
            {
                var t = new Vector2I(x, y);
                if (!IsWalkable(t)) continue;
                int d = (t - c).LengthSquared();
                if (d < bd) { bd = d; best = t; }
            }
            if (best != null) return best;
        }
        return null;
    }

    public List<Vector2I> WalkableNeighbors(Vector2I t)
    {
        var list = new List<Vector2I>();
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
        {
            if (dx == 0 && dy == 0) continue;
            var n = t + new Vector2I(dx, dy);
            if (IsWalkable(n)) list.Add(n);
        }
        return list;
    }

    public TreeObj? FindTree(Vector2I c, int r, Func<TreeObj, bool> pred)
    {
        TreeObj? best = null;
        int bd = int.MaxValue;
        for (int y = Math.Max(0, c.Y - r); y <= Math.Min(Height - 1, c.Y + r); y++)
        for (int x = Math.Max(0, c.X - r); x <= Math.Min(Width - 1, c.X + r); x++)
        {
            var tr = Trees[x, y];
            if (tr == null || !pred(tr)) continue;
            int d = (new Vector2I(x, y) - c).LengthSquared();
            if (d <= r * r && d < bd) { bd = d; best = tr; }
        }
        return best;
    }

    public StoneObj? FindStone(Vector2I c, int r)
    {
        StoneObj? best = null;
        int bd = int.MaxValue;
        for (int y = Math.Max(0, c.Y - r); y <= Math.Min(Height - 1, c.Y + r); y++)
        for (int x = Math.Max(0, c.X - r); x <= Math.Min(Width - 1, c.X + r); x++)
        {
            var st = Stones[x, y];
            if (st == null || WalkableNeighbors(st.Tile).Count == 0) continue;
            int d = (new Vector2I(x, y) - c).LengthSquared();
            if (d <= r * r && d < bd) { bd = d; best = st; }
        }
        return best;
    }

    public int CountSaplings(Vector2I c, int r)
    {
        int n = 0;
        ForEachInRadius(c, r, t => { if (Trees[t.X, t.Y] is { IsMature: false }) n++; });
        return n;
    }

    public bool CanPlantTree(Vector2I t) =>
        InBounds(t) && Ground[t.X, t.Y] is Terrain.Grass or Terrain.Dirt && IsFree(t) && !Roads[t.X, t.Y];

    public Vector2I? FindPlantSpot(Vector2I c, int r, RandomNumberGenerator rng)
    {
        for (int k = 0; k < 30; k++)
        {
            var t = c + new Vector2I(rng.RandiRange(-r, r), rng.RandiRange(-r, r));
            if ((t - c).LengthSquared() > 4 && CanPlantTree(t)) return t;
        }
        return null;
    }

    // ---------------------------------------------------------------- stavění

    bool IsFree(Vector2I t) =>
        Buildings[t.X, t.Y] == null && EntranceOf[t.X, t.Y] == null && Trees[t.X, t.Y] == null &&
        Stones[t.X, t.Y] == null && RoadPlans[t.X, t.Y] == null;

    public bool IsBuildableTile(Vector2I t)
    {
        if (!InBounds(t) || Ground[t.X, t.Y] is Terrain.Water or Terrain.Rock) return false;
        if (!IsFree(t) || Roads[t.X, t.Y]) return false;
        float a = Heights[t.X, t.Y], b = Heights[t.X + 1, t.Y], c = Heights[t.X + 1, t.Y + 1], d = Heights[t.X, t.Y + 1];
        return Math.Max(Math.Max(a, b), Math.Max(c, d)) - Math.Min(Math.Min(a, b), Math.Min(c, d)) <= 16f;
    }

    public bool CanPlace(BuildingDef def, Vector2I origin, List<(Vector2I tile, bool ok)>? results)
    {
        bool all = true;
        for (int y = 0; y < def.Size.Y; y++)
        for (int x = 0; x < def.Size.X; x++)
        {
            var t = origin + new Vector2I(x, y);
            bool ok = IsBuildableTile(t);
            all &= ok;
            results?.Add((t, ok));
        }
        var e = Building.EntranceFor(def, origin);
        bool eok = InBounds(e) && Ground[e.X, e.Y] != Terrain.Water && Buildings[e.X, e.Y] == null &&
                   EntranceOf[e.X, e.Y] == null && Trees[e.X, e.Y] == null && Stones[e.X, e.Y] == null;
        all &= eok;
        results?.Add((e, eok));
        return all;
    }

    public bool CanPlaceRoad(Vector2I t) =>
        InBounds(t) && Ground[t.X, t.Y] != Terrain.Water && Buildings[t.X, t.Y] == null &&
        Stones[t.X, t.Y] == null && Trees[t.X, t.Y] == null && !Roads[t.X, t.Y] && RoadPlans[t.X, t.Y] == null;

    public void OccupyBuilding(Building b)
    {
        foreach (var t in b.Footprint())
        {
            Buildings[t.X, t.Y] = b;
            RefreshTile(t);
        }
        EntranceOf[b.Entrance.X, b.Entrance.Y] = b;
    }

    public void FreeBuilding(Building b)
    {
        foreach (var t in b.Footprint())
        {
            if (Buildings[t.X, t.Y] == b) Buildings[t.X, t.Y] = null;
            RefreshTile(t);
        }
        if (EntranceOf[b.Entrance.X, b.Entrance.Y] == b) EntranceOf[b.Entrance.X, b.Entrance.Y] = null;
    }

    /// <summary>Zarovná vrcholy pod budovou na průměrnou výšku (jako kopání v KaM).</summary>
    public void Flatten(Vector2I origin, Vector2I size)
    {
        float sum = 0;
        int n = 0;
        for (int j = origin.Y; j <= origin.Y + size.Y; j++)
        for (int i = origin.X; i <= origin.X + size.X; i++)
        {
            sum += Heights[i, j];
            n++;
        }
        float avg = sum / n;
        for (int j = origin.Y; j <= origin.Y + size.Y; j++)
        for (int i = origin.X; i <= origin.X + size.X; i++)
            Heights[i, j] = avg;
    }
}
