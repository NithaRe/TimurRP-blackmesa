using Robust.Shared.Prototypes;

namespace Content.Shared._BlackM.XenBiology;

[Prototype("xenExperiment")]
public sealed partial class XenExperimentPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Description { get; private set; } = default!;

    [DataField(required: true)]
    public XenExperimentLevel Level { get; private set; }

    [DataField(required: true)]
    public EntProtoId RequiredSample { get; private set; }

    [DataField(required: true)]
    public int Reward { get; private set; }

    [DataField(required: true)]
    public TimeSpan Duration { get; private set; }

    [DataField]
    public float MinimumStability { get; private set; }

    [DataField]
    public float StabilityCost { get; private set; }

    [DataField]
    public float IncidentChance { get; private set; }

    [DataField]
    public int IncidentCount { get; private set; } = 1;
}

public enum XenExperimentLevel : byte
{
    Safe,
    Standard,
    Dangerous,
    Cascade
}
