using Content.Shared.Tools;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.EngineeringBreakdown;

[Prototype("engineeringBreakdown")]
public sealed partial class BreakdownPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    [DataField(required: true)]
    public EntityWhitelist Whitelist = new();

    [DataField]
    public EntityWhitelist? Blacklist;

    [DataField(required: true)]
    public List<BreakdownRepairStep> Steps = new();

    [DataField]
    public List<BreakdownEffect> Effects = new();

    [DataField]
    public EntProtoId? AttachedEffect;

    [DataField]
    public EntProtoId? BurstEffect;

    [DataField]
    public float BurstInterval = 7f;

    [DataField]
    public SoundSpecifier? BurstSound;

    [DataField]
    public SoundSpecifier? BreakSound;

    [DataField]
    public float ShockChance;

    [DataField]
    public int ShockDamage = 10;

    [DataField]
    public Color Color = Color.Orange;

    [DataField]
    public int Severity = 1;

    [DataField]
    public float EscalationIntensity;

    [DataField]
    public int Reward;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class BreakdownRepairStep
{
    [DataField(required: true)]
    public ProtoId<ToolQualityPrototype> Quality;

    [DataField]
    public float Delay = 3f;

    [DataField]
    public float Fuel;

    [DataField(required: true)]
    public LocId Hint;

    [DataField(required: true)]
    public LocId Done;
}

[Serializable, NetSerializable]
public enum BreakdownEffect : byte
{
    ApcBreakerOff,
    LightBlink,
    PowerDisabled,
    BoltDoor,
    BatteryOffline,
}
