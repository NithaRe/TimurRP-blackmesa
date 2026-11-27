using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._BlackM.CrystalResearch;

[RegisterComponent, NetworkedComponent]
public sealed partial class ZenResearchConsoleComponent : Component;

[Prototype("zenResearchPerk")]
public sealed partial class ZenResearchPerkPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public LocId Name;
    [DataField(required: true)] public LocId Description;
    [DataField] public List<int> Costs = new() { 1, 2, 3 };
    [DataField] public float SpeedPerLevel;
    [DataField] public float PullSpeedPerLevel;
    [DataField] public float InfectionResistancePerLevel;
    [DataField] public float StaminaResistancePerLevel;
    [DataField] public float ThermalResistancePerLevel;
    [DataField] public float RadiationResistancePerLevel;
    [DataField] public float PoisonResistancePerLevel;
}

[Prototype("zenResearchSupply")]
public sealed partial class ZenResearchSupplyPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public LocId Name;
    [DataField(required: true)] public EntProtoId Product;
    [DataField(required: true)] public int Cost;
    [DataField] public int Stock = 3;
    [DataField] public TimeSpan DeliveryTime = TimeSpan.FromSeconds(30);
    [DataField] public ZenResearchSupplyCategory Category;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ZenResearchCrewBonusComponent : Component
{
    [DataField, AutoNetworkedField] public float Speed;
    [DataField, AutoNetworkedField] public float PullSpeed;
    [DataField, AutoNetworkedField] public float InfectionResistance;
    [DataField, AutoNetworkedField] public float StaminaResistance;
    [DataField, AutoNetworkedField] public float ThermalResistance;
    [DataField, AutoNetworkedField] public float RadiationResistance;
    [DataField, AutoNetworkedField] public float PoisonResistance;
}
