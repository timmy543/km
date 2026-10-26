using System.Collections.Generic;
using System.Linq;
using Godot;

namespace km;

public enum BuildState { Digging, Constructing, Done }

public partial class Building : Node2D, IWorkOrder
{
    public BuildingDef Def = null!;
    public Vector2I Origin;
    public BuildState State = BuildState.Digging;
    public bool Removed;
    public bool IsWorking;
    public Worker? Worker;

    public float DigWork, DigTotal = 1f;
    public int StepsDone, StepsReserved;

    // indexováno (int)ResourceType
    public readonly int[] Delivered = new int[Res.Count];
    public readonly int[] Incoming = new int[Res.Count];
    public readonly int[] Stock = new int[Res.Count];
    public readonly int[] Outgoing = new int[Res.Count];

    float time;

    public Vector2I Entrance => EntranceFor(Def, Origin);
    public static Vector2I EntranceFor(BuildingDef def, Vector2I origin) => new(origin.X + def.EntranceX, origin.Y + def.Size.Y);

    public int StepsTotal => Def.CostWood + Def.CostStone;
    public float DigProgress => Mathf.Clamp(DigWork / DigTotal, 0f, 1f);
    public float BuildProgress => StepsTotal == 0 ? 1f : (float)StepsDone / StepsTotal;

    // IWorkOrder
    public Vector2I WorkTile => Entrance;
    public int MaxBuilders => 3;
    public List<Laborer> Builders { get; } = new();
    public bool IsFinished => Removed || State == BuildState.Done;
    public double RetryAt { get; set; }

    public void Setup(BuildingDef def, Vector2I origin)
    {
        Def = def;
        Origin = origin;
        DigTotal = def.Size.X * def.Size.Y * 1.2f;
        UpdatePosition();
    }

    public IEnumerable<Vector2I> Footprint()
    {
        for (int y = 0; y < Def.Size.Y; y++)
        for (int x = 0; x < Def.Size.X; x++)
            yield return Origin + new Vector2I(x, y);
    }

    /// <summary>
    /// Bod pro Y-řazení: leží mezi dlaždicemi za budovou a před ní,
    /// takže postavy u vchodu jsou vykresleny před budovou.
    /// </summary>
    public void UpdatePosition()
    {
        var map = Main.Instance.Map;
        var c = new Vector2(Origin.X + Def.Size.X / 2f, Origin.Y + Def.Size.Y / 2f);
        Position = map.GroundToScreen(c) + new Vector2(0, 4);
        QueueRedraw();
    }

    // ---------------------------------------------------------------- logistika

    public int Cost(ResourceType r) => r switch
    {
        ResourceType.Wood => Def.CostWood,
        ResourceType.Stone => Def.CostStone,
        _ => 0,
    };

    public int ConstructionNeed(ResourceType r) =>
        Removed || State == BuildState.Done ? 0 : Cost(r) - Delivered[(int)r] - Incoming[(int)r];

    public int Offer(ResourceType r)
    {
        if (Removed || State != BuildState.Done) return 0;
        if (!Def.IsStore && !Def.Outputs.Contains(r)) return 0;
        return Stock[(int)r] - Outgoing[(int)r];
    }

    public void Receive(ResourceType r)
    {
        Incoming[(int)r]--;
        if (State == BuildState.Done) Stock[(int)r]++;
        else Delivered[(int)r]++;
        QueueRedraw();
    }

    // ---------------------------------------------------------------- stavba

    public void AddDig(float amount)
    {
        if (State != BuildState.Digging) return;
        DigWork += amount;
        QueueRedraw();
        if (DigWork >= DigTotal)
        {
            State = BuildState.Constructing;
            Main.Instance.OnBuildingDug(this);
        }
    }

    public bool TryReserveStep()
    {
        int delivered = Delivered[(int)ResourceType.Wood] + Delivered[(int)ResourceType.Stone];
        if (State != BuildState.Constructing || StepsDone + StepsReserved >= delivered) return false;
        StepsReserved++;
        return true;
    }

    public void CompleteStep()
    {
        StepsReserved--;
        StepsDone++;
        QueueRedraw();
        if (StepsDone >= StepsTotal && State == BuildState.Constructing)
        {
            State = BuildState.Done;
            Main.Instance.OnBuildingCompleted(this);
        }
    }

    public string StateText() => State switch
    {
        BuildState.Digging => $"Příprava staveniště {Mathf.RoundToInt(DigProgress * 100)} %",
        BuildState.Constructing => $"Stavba {Mathf.RoundToInt(BuildProgress * 100)} %",
        _ => "Hotovo",
    };

    // ---------------------------------------------------------------- vykreslení

    public override void _Process(double delta)
    {
        time += (float)delta;
        if (IsWorking || State != BuildState.Done) QueueRedraw();
    }

    public override void _Draw()
    {
        var map = Main.Instance.Map;
        Vector2 P(float gx, float gy) => map.GroundToScreen(new Vector2(gx, gy)) - Position;
        int x = Origin.X, y = Origin.Y, w = Def.Size.X, h = Def.Size.Y;
        BuildingPainter.Draw(this, Def, P(x, y), P(x + w, y), P(x + w, y + h), P(x, y + h), this, 1f, time);
    }
}
