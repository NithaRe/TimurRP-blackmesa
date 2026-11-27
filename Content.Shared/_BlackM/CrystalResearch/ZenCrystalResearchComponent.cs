using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.CrystalResearch;

[Serializable, NetSerializable]
public enum ZenCrystalStage : byte
{
    Structure,
    Growth,
    Mature,
    Researched,
    Exhausted,
}

[Serializable, NetSerializable]
public enum ZenCrystalExposure : byte
{
    Heat,
    Cold,
    Electricity,
    Radiation,
}

[Serializable, NetSerializable]
public enum ZenGrowthMedium : byte
{
    Mineral,
    Organic,
}

[RegisterComponent, NetworkedComponent]
public sealed partial class ZenCrystalResearchComponent : Component
{
    [DataField] public ZenCrystalStage Stage;
    [DataField] public List<ZenCrystalResearchTask> Tasks = new();
    [DataField] public string CurrentTask = string.Empty;
    [DataField] public int Incidents;
    [DataField] public int PulseAttempts;
    [DataField] public int Step;
    [DataField] public int ScannedStep = -1;
    [DataField] public float Instability;
    [DataField] public float Temperature = 20f;
    [DataField] public float Quality = 1f;
    [DataField] public float GrowthSeconds;
    [DataField] public float GrowthDuration = 210f;
    [DataField] public bool GrowthStarted;
    [DataField] public bool BreakthroughAwarded;
    [DataField] public bool AcceleratedGrowth;
    [DataField] public float AcceleratedGrowthSeconds;
    [DataField] public bool Runaway;
    [DataField] public float BreachRemaining;
    [ViewVariables] public float HazardAccumulator;
    [DataField] public float Energy = 1200f;
    [DataField] public float EnergyCapacity = 1200f;
    [DataField] public float ResearchRemainder;
    [DataField] public List<string> Journal = new();
    [DataField] public EntityUid? LaboratoryStation;
    [DataField] public int SampleNumber;
    [ViewVariables] public EntityUid? ActiveMachine;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ZenCrystalChamberComponent : Component
{
    [DataField] public EntityUid? Crystal;
    [DataField] public float ScanSeconds = 5f;
    [DataField] public float GrowthDuration = 210f;
    [DataField, AutoNetworkedField] public bool LidOpen;
    [DataField, AutoNetworkedField] public bool Resonator;
    [ViewVariables] public ZenCrystalCommand? Operation;
    [ViewVariables] public float OperationRemaining;
    [ViewVariables] public ZenCrystalExposure Exposure;
    [ViewVariables] public string Operator = string.Empty;
    [ViewVariables] public bool Running;
    [ViewVariables] public bool Forced;
    [ViewVariables] public float GenerationAccumulator;
    [ViewVariables] public string Status = string.Empty;
    [ViewVariables] public EntityUid? ActiveEmitter;
    [ViewVariables] public float AppliedDose;
    [ViewVariables] public float ExposureSeconds;
    [ViewVariables] public bool DoseOverloaded;
    [ViewVariables] public float EffectAccumulator;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ZenGrowthMediumComponent : Component
{
    [DataField] public ZenGrowthMedium Medium;
    [DataField, AutoNetworkedField] public bool Spent;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ZenCrystalVisualsComponent : Component
{
    [DataField, AutoNetworkedField] public float Growth;
    [DataField, AutoNetworkedField] public float Energy = 1f;
    [DataField, AutoNetworkedField] public bool Researched;
    [DataField, AutoNetworkedField] public float Instability;
    [DataField, AutoNetworkedField] public float Reaction;
    [DataField, AutoNetworkedField] public bool Active;
    [DataField, AutoNetworkedField] public ZenCrystalExposure Exposure;
    [DataField, AutoNetworkedField] public bool Deposits;
    [DataField, AutoNetworkedField] public bool Overloaded;
}

[Serializable, NetSerializable]
public enum ZenCrystalVisualLayers : byte
{
    Base,
    Glow,
    Deposits,
    Cracks,
}

[Serializable, NetSerializable]
public enum ZenCrystalChamberVisuals : byte
{
    LidOpen,
    Active,
    Sample,
    Deposits,
    Alarm,
    Cracks,
}

[RegisterComponent]
public sealed partial class ZenCrystalRawComponent : Component
{
    [ViewVariables] public bool Split;
}
[Serializable, NetSerializable]
public enum ZenCrystalTaskType : byte
{
    Temperature,
    Electricity,
    Radiation,
    Medium,
    Scrape,
}

[DataDefinition]
public sealed partial class ZenCrystalResearchTask
{
    [DataField] public ZenCrystalTaskType Type;
    [DataField] public float Target;
    [DataField] public float Tolerance;
    [DataField] public ZenGrowthMedium Medium;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class ZenCrystalSpentComponent : Component;
