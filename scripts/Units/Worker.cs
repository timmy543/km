using Godot;

namespace km;

/// <summary>Řemeslník přiřazený k jedné budově (dřevorubec, pilař, kameník).</summary>
public partial class Worker : Unit
{
    public Building? Home;
    public string Status = "Jde do práce";
    TreeObj? targetTree;
    bool leaving;
    readonly RandomNumberGenerator rng = new();

    public Worker() => rng.Randomize();

    protected override Color TunicColor => Kind switch
    {
        UnitKind.Woodcutter => new Color(0.28f, 0.48f, 0.22f),
        UnitKind.Sawyer => new Color(0.78f, 0.50f, 0.18f),
        _ => new Color(0.50f, 0.52f, 0.58f),
    };

    protected override Color HatColor => Kind switch
    {
        UnitKind.Woodcutter => new Color(0.55f, 0.18f, 0.14f),
        UnitKind.Sawyer => new Color(0.35f, 0.25f, 0.15f),
        _ => new Color(0.30f, 0.30f, 0.34f),
    };

    protected override void Think()
    {
        if (Home == null || Home.Removed)
        {
            Leave();
            return;
        }
        if (!Inside)
        {
            if (Tile != Home.Entrance) Walk(Home.Entrance);
            Do(EnterHome);
            return;
        }

        switch (Kind)
        {
            case UnitKind.Woodcutter: ThinkWoodcutter(Home); break;
            case UnitKind.Stonemason: ThinkStonemason(Home); break;
            case UnitKind.Sawyer: ThinkSawyer(Home); break;
        }
    }

    void ThinkWoodcutter(Building home)
    {
        if (home.Stock[(int)ResourceType.Trunk] >= home.Def.MaxStock)
        {
            Status = "Sklad dílny je plný";
            Wait(2f);
            return;
        }

        var tree = Map.FindTree(home.Entrance, home.Def.GatherRadius, t => t.IsMature && t.ReservedBy == null);
        if (tree != null)
        {
            Status = "Kácí strom";
            tree.ReservedBy = this;
            targetTree = tree;
            var tile = tree.Tile;
            Do(ExitHome);
            Walk(tile);
            Wait(4.5f, Pose.Work, Iso.TileCenter(tile) + tree.Jitter);
            Do(() =>
            {
                if (Map.Trees[tile.X, tile.Y] != tree) return false;
                Main.Instance.FellTree(tree);
                targetTree = null;
                Carrying = ResourceType.Trunk;
                return true;
            });
            Walk(home.Entrance);
            Do(EnterHome);
            return;
        }

        // v okolí nejsou vzrostlé stromy – sázet
        if (Map.CountSaplings(home.Entrance, 8) < 10 && Map.FindPlantSpot(home.Entrance, 7, rng) is { } spot)
        {
            Status = "V dosahu nejsou vzrostlé stromy – sází nové";
            Do(ExitHome);
            Walk(spot);
            Wait(2f, Pose.Work);
            Do(() =>
            {
                if (Map.CanPlantTree(spot)) Main.Instance.SpawnTree(spot, false);
                return true;
            });
            Walk(home.Entrance);
            Do(EnterHome);
            return;
        }
        Status = "Čeká, až stromy vyrostou";
        Wait(3f);
    }

    void ThinkStonemason(Building home)
    {
        if (home.Stock[(int)ResourceType.Stone] >= home.Def.MaxStock)
        {
            Status = "Sklad dílny je plný";
            Wait(2f);
            return;
        }

        var stone = Map.FindStone(home.Entrance, home.Def.GatherRadius);
        if (stone == null)
        {
            Status = "V dosahu nejsou žádné skály!";
            Wait(3f);
            return;
        }
        Status = "Láme kámen";
        var tile = stone.Tile;
        Do(ExitHome);
        Walk(Map.WalkableNeighbors(tile).ToArray());
        Wait(5f, Pose.Work, Iso.TileCenter(tile));
        Do(() =>
        {
            if (Map.Stones[tile.X, tile.Y] != stone) return false;
            Main.Instance.MineStone(stone);
            Carrying = ResourceType.Stone;
            return true;
        });
        Walk(home.Entrance);
        Do(EnterHome);
    }

    void ThinkSawyer(Building home)
    {
        int trunk = (int)ResourceType.Trunk, wood = (int)ResourceType.Wood;
        if (home.Stock[trunk] > 0 && home.Stock[wood] <= home.Def.MaxStock - 2)
        {
            Status = "Řeže prkna";
            Do(() =>
            {
                if (home.Removed) return false;
                home.Stock[trunk]--;
                home.IsWorking = true;
                return true;
            });
            Wait(6f, Pose.Work);
            Do(() =>
            {
                home.IsWorking = false;
                if (home.Removed) return false;
                home.Stock[wood] += 2;
                home.QueueRedraw();
                return true;
            });
            return;
        }
        Status = home.Stock[trunk] == 0 ? "Čeká na kmeny" : "Sklad dílny je plný";
        Wait(1f);
    }

    bool ExitHome()
    {
        if (Home == null || Home.Removed) return false;
        Inside = false;
        GridPos = Iso.TileCenter(Home.Entrance);
        return true;
    }

    bool EnterHome()
    {
        if (Home == null || Home.Removed) return false;
        Inside = true;
        if (Carrying is { } r)
        {
            Home.Stock[(int)r]++;
            Carrying = null;
            Home.QueueRedraw();
        }
        return true;
    }

    void Leave()
    {
        if (leaving) return;
        leaving = true;
        Inside = false;
        Carrying = null;
        if (Eco.NearestStore(Tile) is { } store) Walk(store.Entrance);
        Do(Despawn);
    }

    bool Despawn()
    {
        Eco.Workers.Remove(this);
        QueueFree();
        return true;
    }

    protected override void OnFailed()
    {
        if (targetTree != null)
        {
            targetTree.ReservedBy = null;
            targetTree = null;
        }
        if (leaving)
        {
            Despawn();
            return;
        }
        Wait(2f);
    }

    protected override void DrawWork(Vector2 shoulder, float sx, float t)
    {
        if (Kind != UnitKind.Woodcutter)
        {
            base.DrawWork(shoulder, sx, t);
            return;
        }
        // sekera – širší ostří
        float a = -2.1f + (Mathf.Sin(t * 6f) * 0.5f + 0.5f) * 2.2f;
        var dir = new Vector2(Mathf.Cos(a) * sx, Mathf.Sin(a));
        var hand = shoulder + dir * 5f;
        DrawLine(shoulder, hand, Gfx.Shade(TunicColor, 0.75f), 2.2f);
        DrawLine(hand - dir * 2f, hand + dir * 10f, new Color(0.45f, 0.30f, 0.15f), 1.8f);
        var tip = hand + dir * 10f;
        var perp = new Vector2(-dir.Y, dir.X);
        DrawColoredPolygon(new[] { tip, tip + perp * 4f - dir * 1f, tip + perp * 4.5f + dir * 3f, tip + dir * 2f }, new Color(0.62f, 0.63f, 0.68f));
    }
}
