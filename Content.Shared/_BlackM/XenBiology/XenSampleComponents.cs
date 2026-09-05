using Content.Shared.DoAfter;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.XenBiology;

[RegisterComponent]
public sealed partial class XenSampleSourceComponent : Component
{
    /// <summary>
    /// Prototype spawned in the extractor capsule slot after a successful extraction.
    /// </summary>
    [DataField]
    public EntProtoId FilledCapsulePrototype = "XenSampleCapsuleGeneric";

    /// <summary>
    /// Time required to draw a sample from this entity.
    /// </summary>
    [DataField]
    public TimeSpan SampleDelay = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Delay before the same entity can be sampled again.
    /// </summary>
    [DataField]
    public TimeSpan SampleCooldown = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Next world time when this source can be sampled.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan NextSampleTime = TimeSpan.Zero;
}

[RegisterComponent]
public sealed partial class XenSampleExtractorComponent : Component
{
    /// <summary>
    /// Item slot containing the currently loaded sample capsule.
    /// </summary>
    [DataField]
    public string CapsuleSlotId = "xen_sample_capsule";
}

[RegisterComponent]
public sealed partial class XenSampleCapsuleComponent : Component
{
    /// <summary>
    /// Whether this capsule already contains a collected sample.
    /// </summary>
    [DataField]
    public bool Filled;
}

[RegisterComponent]
public sealed partial class ScientificCrusherInputComponent : Component
{
    /// <summary>
    /// Research points awarded when this input is processed.
    /// </summary>
    [DataField]
    public int Points;

    /// <summary>
    /// Chance that processing this input causes a minor crusher incident.
    /// </summary>
    [DataField]
    public float IncidentChance;

    /// <summary>
    /// Stack produced when this input is processed.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<StackPrototype> MaterialStack;

    /// <summary>
    /// Base number of material units produced from an unprocessed input.
    /// </summary>
    [DataField]
    public int MaterialAmount = 1;

    /// <summary>
    /// Prototype returned after processing a reusable sample container.
    /// </summary>
    [DataField]
    public EntProtoId? EmptyResult;
}

[RegisterComponent]
public sealed partial class XenExperimentResultComponent : Component
{
    /// <summary>
    /// Experiment that recorded this result.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public ProtoId<XenExperimentPrototype>? Experiment;

    /// <summary>
    /// Research data awarded by the crusher.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public int RecordedData;

    /// <summary>
    /// Number of material units produced by the crusher.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public int MaterialAmount;

    /// <summary>
    /// Quality of the recorded experiment result.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public XenExperimentResultQuality Quality;
}

[Serializable, NetSerializable]
public enum XenExperimentResultQuality : byte
{
    Analyzed,
    Unstable,
    Spoiled
}

[Serializable, NetSerializable]
public enum XenExperimentResultVisuals : byte
{
    Quality
}

[Serializable, NetSerializable]
public sealed partial class XenSampleExtractDoAfterEvent : SimpleDoAfterEvent;
