using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.XenBiology;

[RegisterComponent]
public sealed partial class XenExperimentConsoleComponent : Component
{
    public const string DefaultSampleSlotId = "xen_experiment_sample";
    public const string DefaultCapacitorSlotId = "xen_experiment_capacitor";

    [DataField]
    public string SampleSlotId = DefaultSampleSlotId;

    [DataField]
    public string CapacitorSlotId = DefaultCapacitorSlotId;

    [DataField]
    public float Stability = 100f;

    [DataField]
    public float StabilityRecoveryPerSecond = 0.35f;

    [DataField]
    public EntProtoId ElectricalDischarge = "XenExperimentElectricalDischarge";

    [DataField]
    public EntProtoId RadiationEffect = "XenExperimentRadiationPulse";

    [DataField]
    public List<EntProtoId> XenMobs = new()
    {
        "MobHeadcrabBlackM",
        "MobHoundeyeBlackM",
        "MobXenInfected"
    };

    [DataField]
    public List<EntProtoId> DangerousXenMobs = new()
    {
        "MobHoundeyeBlackM",
        "MobVortigauntBlackM",
        "MobXenInfected"
    };

    [ViewVariables(VVAccess.ReadOnly)]
    public ProtoId<XenExperimentPrototype>? RunningExperiment;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan ExperimentEndTime;
}

[Serializable, NetSerializable]
public enum XenExperimentConsoleUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class XenExperimentStartMessage(string experimentId) : BoundUserInterfaceMessage
{
    public readonly string ExperimentId = experimentId;
}

[Serializable, NetSerializable]
public sealed class XenExperimentConsoleBoundUserInterfaceState(
    int data,
    float stability,
    string? samplePrototype,
    bool sampleProcessed,
    string? capacitorPrototype,
    float incidentChanceMultiplier,
    string? runningExperiment,
    int remainingSeconds,
    bool powered,
    bool connected) : BoundUserInterfaceState
{
    public readonly int Data = data;
    public readonly float Stability = stability;
    public readonly string? SamplePrototype = samplePrototype;
    public readonly bool SampleProcessed = sampleProcessed;
    public readonly string? CapacitorPrototype = capacitorPrototype;
    public readonly float IncidentChanceMultiplier = incidentChanceMultiplier;
    public readonly string? RunningExperiment = runningExperiment;
    public readonly int RemainingSeconds = remainingSeconds;
    public readonly bool Powered = powered;
    public readonly bool Connected = connected;
}
