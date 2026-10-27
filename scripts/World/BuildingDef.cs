using System;
using Godot;

namespace km;

public enum WallStyle { Timber, Stone, Logs }
public enum RoofShape { GableSide, GableFront }   // hřeben rovnoběžně s fasádou / štít čelem k divákovi
public enum RoofMaterial { Tiles, Thatch, Slate }

public sealed class BuildingDef
{
    public string Name = "";
    public string Description = "";
    public Vector2I Size = new(2, 2);
    public int EntranceX;               // vchod je pod fasádou (jižní strana), posun podél osy X
    public int CostWood, CostStone;
    public UnitKind? Worker;
    public ResourceType[] Inputs = Array.Empty<ResourceType>();
    public ResourceType[] Outputs = Array.Empty<ResourceType>();
    public int MaxStock = 5;
    public int GatherRadius;            // dosah řemeslníka (dřevorubec, kameník), 0 = nesbírá venku
    public bool IsStore;
    public bool Chimney;
    public bool WideDoor;
    public WallStyle Walls = WallStyle.Timber;
    public RoofShape Shape = RoofShape.GableSide;
    public RoofMaterial RoofMat = RoofMaterial.Tiles;
    public Color Wall = new(0.88f, 0.82f, 0.68f);
    public Color Roof = new(0.55f, 0.36f, 0.18f);
    public Color Sign = Colors.White;
    public float WallH = 32f, RoofH = 32f;

    public static readonly BuildingDef Storehouse = new()
    {
        Name = "Sklad",
        Description = "Uskladňuje suroviny. Odtud nosiči berou materiál na stavby.",
        Size = new(3, 2), EntranceX = 1, CostWood = 6, CostStone = 5,
        IsStore = true, WideDoor = true,
        Walls = WallStyle.Stone, Shape = RoofShape.GableSide, RoofMat = RoofMaterial.Tiles,
        Wall = new(0.66f, 0.62f, 0.56f), Roof = new(0.56f, 0.30f, 0.19f), Sign = new(0.95f, 0.78f, 0.25f),
        WallH = 52f, RoofH = 40f,
    };

    public static readonly BuildingDef Woodcutter = new()
    {
        Name = "Dřevorubec",
        Description = "Kácí stromy v okolí a sází nové.",
        Size = new(2, 2), EntranceX = 0, CostWood = 3, CostStone = 2,
        Worker = UnitKind.Woodcutter, Outputs = new[] { ResourceType.Trunk }, GatherRadius = 12,
        Walls = WallStyle.Logs, Shape = RoofShape.GableFront, RoofMat = RoofMaterial.Thatch,
        Wall = new(0.50f, 0.34f, 0.20f), Roof = new(0.74f, 0.62f, 0.36f), Sign = new(0.30f, 0.55f, 0.22f),
        WallH = 42f, RoofH = 36f,
    };

    public static readonly BuildingDef Sawmill = new()
    {
        Name = "Pila",
        Description = "Z jednoho kmene udělá dvě prkna.",
        Size = new(2, 2), EntranceX = 1, CostWood = 4, CostStone = 3,
        Worker = UnitKind.Sawyer, Inputs = new[] { ResourceType.Trunk }, Outputs = new[] { ResourceType.Wood },
        Chimney = true, Walls = WallStyle.Timber, Shape = RoofShape.GableSide, RoofMat = RoofMaterial.Tiles,
        Roof = new(0.68f, 0.27f, 0.17f), Sign = new(0.88f, 0.68f, 0.38f),
        WallH = 46f, RoofH = 38f,
    };

    public static readonly BuildingDef Quarry = new()
    {
        Name = "Kamenolom",
        Description = "Kameník láme kámen ze skal v okolí.",
        Size = new(2, 2), EntranceX = 0, CostWood = 3, CostStone = 1,
        Worker = UnitKind.Stonemason, Outputs = new[] { ResourceType.Stone }, GatherRadius = 14,
        Walls = WallStyle.Stone, Shape = RoofShape.GableFront, RoofMat = RoofMaterial.Slate,
        Wall = new(0.62f, 0.60f, 0.56f), Roof = new(0.40f, 0.42f, 0.48f), Sign = new(0.70f, 0.70f, 0.72f),
        WallH = 42f, RoofH = 36f,
    };

    public static readonly BuildingDef[] Buildable = { Storehouse, Woodcutter, Sawmill, Quarry };
}
