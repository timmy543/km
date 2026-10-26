using Godot;

namespace km;

public enum ResourceType { Trunk, Wood, Stone }

public enum UnitKind { Serf, Laborer, Woodcutter, Sawyer, Stonemason }

public static class Res
{
    public const int Count = 3;
    public static readonly ResourceType[] All = { ResourceType.Trunk, ResourceType.Wood, ResourceType.Stone };
    public static readonly ResourceType[] BuildMaterials = { ResourceType.Wood, ResourceType.Stone };

    public static string Name(ResourceType r) => r switch
    {
        ResourceType.Trunk => "Kmeny",
        ResourceType.Wood => "Dřevo",
        _ => "Kámen",
    };

    public static Color Tint(ResourceType r) => r switch
    {
        ResourceType.Trunk => new Color(0.45f, 0.30f, 0.16f),
        ResourceType.Wood => new Color(0.86f, 0.70f, 0.42f),
        _ => new Color(0.62f, 0.62f, 0.60f),
    };

    public static string UnitName(UnitKind k) => k switch
    {
        UnitKind.Serf => "Nosič",
        UnitKind.Laborer => "Stavitel",
        UnitKind.Woodcutter => "Dřevorubec",
        UnitKind.Sawyer => "Pilař",
        _ => "Kameník",
    };
}

/// <summary>Cokoliv, na čem pracují stavitelé – plán silnice nebo staveniště.</summary>
public interface IWorkOrder
{
    Vector2I WorkTile { get; }
    int MaxBuilders { get; }
    System.Collections.Generic.List<Laborer> Builders { get; }
    bool IsFinished { get; }
    double RetryAt { get; set; }
}

public sealed class RoadPlan : IWorkOrder
{
    public Vector2I Tile;
    public bool Done, Cancelled;
    public Vector2I WorkTile => Tile;
    public int MaxBuilders => 1;
    public System.Collections.Generic.List<Laborer> Builders { get; } = new();
    public bool IsFinished => Done || Cancelled;
    public double RetryAt { get; set; }
}
