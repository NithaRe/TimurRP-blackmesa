using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.CrystalResearch;

[Serializable, NetSerializable]
public enum ZenCrystalUiKey : byte
{
    Chamber,
    Console,
    Emitter,
}

[Serializable, NetSerializable]
public enum ZenCrystalCommand : byte
{
    StartGrowth,
    Finalize,
    Stop,
    Generate,
    AccelerateGrowth,
    Quench,
}

[Serializable, NetSerializable]
public sealed class ZenCrystalCommandMessage(
    ZenCrystalCommand command,
    int value = 0,
    NetEntity? sample = null) : BoundUserInterfaceMessage
{
    public readonly ZenCrystalCommand Command = command;
    public readonly int Value = value;
    public readonly NetEntity? Sample = sample;
}

[Serializable, NetSerializable]
public sealed class ZenCrystalChamberState : BoundUserInterfaceState
{
    public bool Powered;
    public bool Connected;
    public bool HasCrystal;
    public NetEntity? Sample;
    public bool Resonator;
    public string Name = string.Empty;
    public ZenCrystalStage Stage;
    public int Step;
    public int Tasks;
    public bool Scanned;
    public float Instability;
    public float Quality;
    public float Growth;
    public float Energy;
    public float EnergyCapacity;
    public bool NeedsService;
    public bool GrowthStarted;
    public bool Running;
    public bool Forced;
    public bool AcceleratedGrowth;
    public float BonusProgress;
    public bool Runaway;
    public string Hazard = string.Empty;
    public float Temperature;
    public ZenCrystalCommand? Operation;
    public int RemainingSeconds;
    public string Status = string.Empty;
    public bool LidOpen;
    public bool MediumPresent;
    public bool MediumSpent;
    public string MediumName = string.Empty;
    public bool Exposing;
    public float Dose;
    public string Reaction = string.Empty;
}

[Serializable, NetSerializable]
public sealed class ZenResearchPurchaseMessage(string id, bool perk) : BoundUserInterfaceMessage
{
    public readonly string Id = id;
    public readonly bool Perk = perk;
}

[Serializable, NetSerializable]
public sealed record ZenResearchPerkEntry(string Id, string Name, string Description, int Level, int Maximum, int Cost);

[Serializable, NetSerializable]
public enum ZenResearchSupplyCategory : byte
{
    Materials,
    Medical,
    Armory,
}

[Serializable, NetSerializable]
public sealed record ZenResearchSupplyEntry(
    string Id,
    string Name,
    string Product,
    int Cost,
    int Stock,
    int Seconds,
    ZenResearchSupplyCategory Category = ZenResearchSupplyCategory.Materials);

[Serializable, NetSerializable]
public sealed class ZenResearchConsoleState : BoundUserInterfaceState
{
    public bool Powered;
    public bool Connected;
    public int Breakthroughs;
    public int Points;
    public List<ZenResearchPerkEntry> Perks = new();
    public List<ZenResearchSupplyEntry> Supplies = new();
    public List<ZenResearchOrderEntry> Deliveries = new();
    public List<ZenResearchSampleEntry> Samples = new();
}

[Serializable, NetSerializable]
public sealed record ZenResearchOrderEntry(int Number, string Name, int Seconds, bool Delivered);

[Serializable, NetSerializable]
public sealed record ZenResearchSampleEntry(int Number, string Name, string Stage, string Task, string Journal);

[Serializable, NetSerializable]
public sealed class ZenCrystalEmitterCommandMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ZenCrystalEmitterState : BoundUserInterfaceState
{
    public ZenCrystalExposure Exposure;
    public bool Powered;
    public bool Active;
    public bool Connected;
    public float Power;
    public float SampleTemperature;
    public bool HasSample;
    public float Dose;
    public float Instability;
    public float Consumable;
    public float ConsumableCapacity;
    public string Target = string.Empty;
    public string Hazard = string.Empty;
    public string Status = string.Empty;
}
