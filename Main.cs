using System.Linq;
using Godot;

namespace km;

public enum ToolMode { None, Build, Road, Demolish }

/// <summary>
/// Jedna rozehraná hra: vygeneruje mapu, postaví scénu, založí osadu a řeší vstup hráče.
/// Vytváří ji App – buď jako hru, nebo jako živé pozadí hlavního menu.
/// </summary>
public partial class Main : Node2D
{
    public const int MapSize = 96;

    public static Main Instance { get; private set; } = null!;

    // parametry nastavené před přidáním do stromu
    public int Seed;
    public bool RevealAll;
    public bool MenuBackground;   // bez UI a vstupu, kamera se pomalu posouvá, vesnice se staví sama

    public GameMap Map { get; private set; } = null!;
    public Economy Eco { get; } = new();
    public TerrainView Terrain { get; private set; } = null!;
    public RoadLayer Roads { get; private set; } = null!;
    public Node2D Objects { get; private set; } = null!;
    public CameraRig Cam { get; private set; } = null!;

    public ToolMode Tool { get; private set; }
    public BuildingDef? BuildDef { get; private set; }
    public Building? Selected { get; set; }
    public Vector2I HoverTile { get; private set; }
    public bool ShowPaths { get; set; }

    readonly RandomNumberGenerator rng = new();
    PauseMenu? pauseMenu;
    bool roadDrag;
    Vector2I lastRoadTile;
    public static readonly double[] Speeds = { 1, 2, 3, 5 };
    int speedIdx;

    public bool Paused => Engine.TimeScale == 0;
    public int SpeedIndex => speedIdx;

    public void TogglePause() => Engine.TimeScale = Paused ? Speeds[speedIdx] : 0;

    public void SetSpeed(int idx)
    {
        speedIdx = Mathf.Clamp(idx, 0, Speeds.Length - 1);
        Engine.TimeScale = Speeds[speedIdx];
    }

    public override void _Ready()
    {
        Instance = this;
        rng.Seed = (ulong)Seed;
        GD.Print($"km: seed mapy {Seed}");

        ArtFactory.Init(Seed);
        TerrainArt.Init(Seed);
        Map = new GameMap(MapSize, MapSize);
        Map.Generate(Seed);

        Terrain = new TerrainView { ZIndex = -20 };
        AddChild(Terrain);
        Roads = new RoadLayer { ZIndex = -15 };
        AddChild(Roads);
        Objects = new Node2D { YSortEnabled = true };
        AddChild(Objects);
        AddChild(new FogLayer { ZIndex = 15 });
        if (!MenuBackground) AddChild(new Overlay { ZIndex = 20 });

        foreach (var t in Map.GenTrees) SpawnTree(t, true);
        foreach (var t in Map.GenStones) SpawnStone(t);
        foreach (var t in Map.GenDecor) SpawnDecor(t);
        Terrain.Init(Map);

        Cam = new CameraRig
        {
            Bounds = new Rect2(0, -60, MapSize * Iso.Tile, MapSize * Iso.Tile + 260),
            Locked = MenuBackground,
        };
        AddChild(Cam);
        AddChild(new PostFx());
        if (!MenuBackground)
        {
            AddChild(new Hud { Layer = 2 });
            pauseMenu = new PauseMenu();
            AddChild(pauseMenu);
            AddChild(new DevTools());
        }

        if (RevealAll || MenuBackground) Map.Reveal(new Vector2(MapSize, MapSize) * 0.5f, MapSize);
        FoundSettlement();
        if (MenuBackground)
        {
            AutoBuild();
            Cam.ApplyZoom(1.1f);
        }
    }

    public void OpenPauseMenu(bool settings = false) => pauseMenu?.Open(settings);

    /// <summary>Naplánuje dřevorubce, pilu, kamenolom a silnice ke skladu (demo a pozadí menu).</summary>
    public void AutoBuild()
    {
        var store = Eco.NearestStore(Map.Center);
        if (store == null) return;
        foreach (var (def, offset) in new[]
                 {
                     (BuildingDef.Woodcutter, new Vector2I(-6, 3)),
                     (BuildingDef.Sawmill, new Vector2I(3, 5)),
                     (BuildingDef.Quarry, new Vector2I(6, -5)),
                 })
        {
            var b = PlaceNear(def, Map.Center + offset);
            if (b == null) continue;
            var path = Map.FindPath(b.Entrance, store.Entrance);
            if (path == null) continue;
            for (int i = 0; i < path.Count; i++)
            {
                // diagonální krok doplnit rohovou dlaždicí, ať je silnice souvislá
                if (i > 0 && path[i].X != path[i - 1].X && path[i].Y != path[i - 1].Y)
                {
                    var corner = new Vector2I(path[i].X, path[i - 1].Y);
                    if (!Map.CanPlaceRoad(corner)) corner = new Vector2I(path[i - 1].X, path[i].Y);
                    PlaceRoadPlan(corner);
                }
                PlaceRoadPlan(path[i]);
            }
        }
    }

    Building? PlaceNear(BuildingDef def, Vector2I c)
    {
        for (int r = 0; r <= 10; r++)
        for (int y = c.Y - r; y <= c.Y + r; y++)
        for (int x = c.X - r; x <= c.X + r; x++)
        {
            var o = new Vector2I(x, y);
            if (Map.CanPlace(def, o, null)) return PlaceBuilding(def, o);
        }
        return null;
    }

    void FoundSettlement()
    {
        var def = BuildingDef.Storehouse;
        var origin = Map.Center - new Vector2I(1, 1);
        Map.Reveal(Iso.TileCenter(Map.Center), 16f);
        var store = PlaceBuilding(def, origin, force: true)!;
        store.DigWork = store.DigTotal;
        store.State = BuildState.Done;
        store.StepsDone = store.StepsTotal;
        Map.Flatten(store.Origin, def.Size);
        OnTerrainChanged(new Rect2I(store.Origin, def.Size));
        if (Map.RoadPlans[store.Entrance.X, store.Entrance.Y] is { } plan) CompleteRoad(plan);

        store.Stock[(int)ResourceType.Wood] = 30;
        store.Stock[(int)ResourceType.Stone] = 25;
        store.Stock[(int)ResourceType.Trunk] = 4;

        for (int i = 0; i < 8; i++) Recruit(UnitKind.Serf);
        for (int i = 0; i < 4; i++) Recruit(UnitKind.Laborer);

        Cam.Position = Map.GroundToScreen(Iso.TileCenter(store.Entrance));
    }

    float revealTimer, menuTime;

    public override void _Process(double delta)
    {
        Eco.Update((float)delta);

        revealTimer -= (float)delta;
        if (revealTimer <= 0f)
        {
            revealTimer = 0.5f;
            foreach (var u in Eco.AllUnits)
                if (u.Visible) Map.Reveal(u.GridPos, 7f);
        }

        if (MenuBackground)
        {
            // pomalý průlet nad vesnicí
            menuTime += (float)delta;
            var c = Map.GroundToScreen(Iso.TileCenter(Map.Center));
            Cam.JumpTo(c + new Vector2(Mathf.Cos(menuTime * 0.05f) * 380f, Mathf.Sin(menuTime * 0.05f) * 240f));
            return;
        }
        HoverTile = Map.ScreenToTile(GetGlobalMousePosition());
    }

    // ---------------------------------------------------------------- vstup

    public static Vector2I BuildOrigin(BuildingDef def, Vector2I hover) => hover - def.Size / 2;

    public void SelectTool(ToolMode mode, BuildingDef? def = null)
    {
        if (Tool == mode && BuildDef == def) mode = ToolMode.None;
        Tool = mode;
        BuildDef = mode == ToolMode.Build ? def : null;
        roadDrag = false;
        if (mode != ToolMode.None) Selected = null;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (MenuBackground) return;
        switch (e)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } mb:
                if (mb.Pressed) OnLeftClick();
                else roadDrag = false;
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true }:
                SelectTool(ToolMode.None);
                Selected = null;
                break;
            case InputEventMouseMotion when roadDrag:
                HoverTile = Map.ScreenToTile(GetGlobalMousePosition());
                PaintRoadTo(HoverTile);
                break;
            case InputEventKey { Pressed: true, Echo: false } k:
                OnKey(k.Keycode);
                break;
        }
    }

    void OnKey(Key key)
    {
        switch (key)
        {
            case Key.Escape:
                if (Tool != ToolMode.None || Selected != null)
                {
                    SelectTool(ToolMode.None);
                    Selected = null;
                }
                else pauseMenu?.Open();
                break;
            case Key.R: SelectTool(ToolMode.Road); break;
            case Key.X or Key.Delete: SelectTool(ToolMode.Demolish); break;
            case >= Key.Key1 and <= Key.Key9:
                int i = (int)(key - Key.Key1);
                if (i < BuildingDef.Buildable.Length) SelectTool(ToolMode.Build, BuildingDef.Buildable[i]);
                break;
            case Key.P: ShowPaths = !ShowPaths; break;
            case Key.Space: TogglePause(); break;
            case Key.Equal or Key.KpAdd or Key.Plus: SetSpeed(speedIdx + 1); break;
            case Key.Minus or Key.KpSubtract: SetSpeed(speedIdx - 1); break;
        }
    }

    void OnLeftClick()
    {
        HoverTile = Map.ScreenToTile(GetGlobalMousePosition());
        var t = HoverTile;
        if (!Map.InBounds(t)) return;
        switch (Tool)
        {
            case ToolMode.Build when BuildDef != null:
                PlaceBuilding(BuildDef, BuildOrigin(BuildDef, t));
                break;
            case ToolMode.Road:
                roadDrag = true;
                lastRoadTile = t;
                PlaceRoadPlan(t);
                break;
            case ToolMode.Demolish:
                DemolishAt(t);
                break;
            default:
                Selected = Map.Buildings[t.X, t.Y] ?? Map.EntranceOf[t.X, t.Y];
                break;
        }
    }

    /// <summary>Tažením se pokládá souvislá silnice (4-směrně, bez děr).</summary>
    void PaintRoadTo(Vector2I target)
    {
        if (!Map.InBounds(target)) return;
        var cur = lastRoadTile;
        while (cur != target)
        {
            var d = target - cur;
            if (Mathf.Abs(d.X) >= Mathf.Abs(d.Y)) cur.X += Mathf.Sign(d.X);
            else cur.Y += Mathf.Sign(d.Y);
            PlaceRoadPlan(cur);
        }
        lastRoadTile = target;
    }

    // ---------------------------------------------------------------- svět

    public Building? PlaceBuilding(BuildingDef def, Vector2I origin, bool force = false)
    {
        if (!force && !Map.CanPlace(def, origin, null)) return null;
        var b = new Building();
        b.Setup(def, origin);
        foreach (var t in b.Footprint().Append(b.Entrance))
        {
            RemoveDecor(t);
            if (force && Map.Trees[t.X, t.Y] is { } tree) RemoveTree(tree);
            if (force && Map.Stones[t.X, t.Y] is { } stone) RemoveStone(stone);
        }
        Map.OccupyBuilding(b);
        Map.Reveal(new Vector2(origin.X + def.Size.X / 2f, origin.Y + def.Size.Y / 2f), 8f);
        Objects.AddChild(b);
        Eco.Buildings.Add(b);
        Eco.Orders.Add(b);
        var e = b.Entrance;
        if (!Map.Roads[e.X, e.Y] && Map.RoadPlans[e.X, e.Y] == null) PlaceRoadPlan(e);
        return b;
    }

    public void Demolish(Building b)
    {
        if (b.Removed) return;
        b.Removed = true;
        Map.FreeBuilding(b);
        Eco.Buildings.Remove(b);
        foreach (var l in b.Builders.ToList()) l.ReleaseOrder();
        if (Selected == b) Selected = null;
        b.QueueFree();
    }

    void DemolishAt(Vector2I t)
    {
        if (Map.RoadPlans[t.X, t.Y] is { } plan)
        {
            plan.Cancelled = true;
            Map.RoadPlans[t.X, t.Y] = null;
            Roads.QueueRedraw();
        }
        else if (Map.Buildings[t.X, t.Y] is { } b) Demolish(b);
        else if (Map.Roads[t.X, t.Y])
        {
            Map.Roads[t.X, t.Y] = false;
            Map.RefreshTile(t);
            Roads.QueueRedraw();
        }
    }

    public void PlaceRoadPlan(Vector2I t)
    {
        if (!Map.CanPlaceRoad(t)) return;
        var plan = new RoadPlan { Tile = t };
        Map.RoadPlans[t.X, t.Y] = plan;
        Eco.Orders.Add(plan);
        Roads.QueueRedraw();
    }

    public void CompleteRoad(RoadPlan plan)
    {
        var t = plan.Tile;
        plan.Done = true;
        if (Map.RoadPlans[t.X, t.Y] == plan) Map.RoadPlans[t.X, t.Y] = null;
        Map.Roads[t.X, t.Y] = true;
        RemoveDecor(t);
        Map.RefreshTile(t);
        Roads.QueueRedraw();
    }

    public void OnBuildingDug(Building b)
    {
        Map.Flatten(b.Origin, b.Def.Size);
        OnTerrainChanged(new Rect2I(b.Origin, b.Def.Size));
    }

    public void OnBuildingCompleted(Building b)
    {
        if (b.Def.Worker is { } kind) SpawnWorker(kind, b);
    }

    /// <summary>Po změně výšek přestaví terén a posune objekty v okolí.</summary>
    void OnTerrainChanged(Rect2I area)
    {
        Terrain.Rebuild();
        var grown = area.Grow(2);
        for (int y = grown.Position.Y; y < grown.End.Y; y++)
        for (int x = grown.Position.X; x < grown.End.X; x++)
        {
            var t = new Vector2I(x, y);
            if (!Map.InBounds(t)) continue;
            Map.Trees[x, y]?.UpdatePosition();
            Map.Stones[x, y]?.UpdatePosition();
            if (Map.Decor[x, y] is { } d) d.Position = Map.GroundToScreen(Iso.TileCenter(t));
        }
        foreach (var b in Eco.Buildings) b.UpdatePosition();
        Roads.QueueRedraw();
    }

    public void SpawnTree(Vector2I t, bool mature)
    {
        RemoveDecor(t);
        var arts = rng.Randf() < 0.35f ? ArtFactory.Conifers : ArtFactory.Deciduous;
        var tree = new TreeObj();
        tree.Setup(t, arts[rng.RandiRange(0, arts.Length - 1)], mature, rng);
        Map.Trees[t.X, t.Y] = tree;
        Map.RefreshTile(t);
        Objects.AddChild(tree);
    }

    void RemoveTree(TreeObj tree)
    {
        Map.Trees[tree.Tile.X, tree.Tile.Y] = null;
        Map.RefreshTile(tree.Tile);
        tree.QueueFree();
    }

    public void FellTree(TreeObj tree)
    {
        var pos = tree.Position;
        RemoveTree(tree);
        var stump = new Sprite2D { Texture = ArtFactory.Stump.Tex, Offset = ArtFactory.Stump.Offset, Position = pos };
        Objects.AddChild(stump);
        var tw = stump.CreateTween();
        tw.TweenInterval(40.0);
        tw.TweenProperty(stump, "modulate:a", 0f, 3.0);
        tw.TweenCallback(Callable.From(stump.QueueFree));
    }

    void SpawnStone(Vector2I t)
    {
        var stone = new StoneObj();
        stone.Setup(t, ArtFactory.Stones[rng.RandiRange(0, ArtFactory.Stones.Length - 1)], rng.RandiRange(5, 9), rng);
        Map.Stones[t.X, t.Y] = stone;
        Map.RefreshTile(t);
        Objects.AddChild(stone);
    }

    void RemoveStone(StoneObj stone)
    {
        Map.Stones[stone.Tile.X, stone.Tile.Y] = null;
        Map.RefreshTile(stone.Tile);
        stone.QueueFree();
    }

    public void MineStone(StoneObj stone)
    {
        stone.Amount--;
        if (stone.Amount <= 0) RemoveStone(stone);
        else stone.ApplyScale();
    }

    void SpawnDecor(Vector2I t)
    {
        var art = ArtFactory.Decor[rng.RandiRange(0, ArtFactory.Decor.Length - 1)];
        var jitter = new Vector2(rng.RandfRange(-0.25f, 0.25f), rng.RandfRange(-0.25f, 0.25f));
        var s = new Sprite2D
        {
            Texture = art.Tex, Offset = art.Offset, FlipH = rng.Randf() < 0.5f,
            Position = Map.GroundToScreen(Iso.TileCenter(t) + jitter),
        };
        Map.Decor[t.X, t.Y] = s;
        Objects.AddChild(s);
    }

    void RemoveDecor(Vector2I t)
    {
        if (!Map.InBounds(t) || Map.Decor[t.X, t.Y] is not { } d) return;
        Map.Decor[t.X, t.Y] = null;
        d.QueueFree();
    }

    // ---------------------------------------------------------------- jednotky

    public void Recruit(UnitKind kind)
    {
        var store = Eco.NearestStore(Map.Center);
        if (store == null) return;
        Unit u = kind == UnitKind.Serf ? new Serf() : new Laborer();
        AddUnit(u, store.Entrance);
    }

    void SpawnWorker(UnitKind kind, Building home)
    {
        var w = new Worker { Kind = kind, Home = home };
        home.Worker = w;
        var start = Eco.NearestStore(home.Entrance)?.Entrance ?? home.Entrance;
        AddUnit(w, start);
    }

    void AddUnit(Unit u, Vector2I tile)
    {
        u.GridPos = Iso.TileCenter(tile) + new Vector2(rng.RandfRange(-0.3f, 0.3f), rng.RandfRange(-0.3f, 0.3f));
        switch (u)
        {
            case Serf s: Eco.Serfs.Add(s); break;
            case Laborer l: Eco.Laborers.Add(l); break;
            case Worker w: Eco.Workers.Add(w); break;
        }
        Objects.AddChild(u);
    }
}
