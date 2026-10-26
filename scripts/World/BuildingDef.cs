using System;
using Godot;

namespace km;

public sealed class BuildingDef
{
    public string Name = "";
    public string Description = "";
    public Vector2I Size = new(2, 2);
    public int EntranceX;               // vchod je před levou přední stěnou, posun podél osy X
    public int CostWood, CostStone;
    public UnitKind? Worker;
    public ResourceType[] Inputs = Array.Empty<ResourceType>();
    public ResourceType[] Outputs = Array.Empty<ResourceType>();
    public int MaxStock = 5;
    public int GatherRadius;            // dosah řemeslníka (dřevorubec, kameník), 0 = nesbírá venku
    public bool IsStore;
    public bool Chimney;
    public bool WideDoor;
    public Color Wall = new(0.82f, 0.74f, 0.58f);
    public Color Roof = new(0.55f, 0.36f, 0.18f);
    public Color Sign = Colors.White;
    public float WallH = 26f, RoofH = 24f;

    public static readonly BuildingDef Storehouse = new()
    {
        Name = "Sklad",
        Description = "Uskladňuje suroviny. Odtud nosiči berou materiál na stavby.",
        Size = new(3, 2), EntranceX = 1, CostWood = 6, CostStone = 5,
        IsStore = true, WideDoor = true,
        Wall = new(0.80f, 0.72f, 0.56f), Roof = new(0.40f, 0.29f, 0.20f), Sign = new(0.95f, 0.78f, 0.25f),
        WallH = 32f, RoofH = 28f,
    };

    public static readonly BuildingDef Woodcutter = new()
    {
        Name = "Dřevorubec",
        Description = "Kácí stromy v okolí a sází nové.",
        Size = new(2, 2), EntranceX = 0, CostWood = 3, CostStone = 2,
        Worker = UnitKind.Woodcutter, Outputs = new[] { ResourceType.Trunk }, GatherRadius = 12,
        Roof = new(0.58f, 0.42f, 0.20f), Sign = new(0.30f, 0.55f, 0.22f),
    };

    public static readonly BuildingDef Sawmill = new()
    {
        Name = "Pila",
        Description = "Z jednoho kmene udělá dvě prkna.",
        Size = new(2, 2), EntranceX = 1, CostWood = 4, CostStone = 3,
        Worker = UnitKind.Sawyer, Inputs = new[] { ResourceType.Trunk }, Outputs = new[] { ResourceType.Wood },
        Chimney = true, Roof = new(0.64f, 0.26f, 0.17f), Sign = new(0.88f, 0.68f, 0.38f),
    };

    public static readonly BuildingDef Quarry = new()
    {
        Name = "Kamenolom",
        Description = "Kameník láme kámen ze skal v okolí.",
        Size = new(2, 2), EntranceX = 0, CostWood = 3, CostStone = 1,
        Worker = UnitKind.Stonemason, Outputs = new[] { ResourceType.Stone }, GatherRadius = 14,
        Wall = new(0.78f, 0.74f, 0.66f), Roof = new(0.42f, 0.44f, 0.50f), Sign = new(0.70f, 0.70f, 0.72f),
    };

    public static readonly BuildingDef[] Buildable = { Storehouse, Woodcutter, Sawmill, Quarry };
}
