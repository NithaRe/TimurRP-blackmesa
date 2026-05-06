using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._BlackM.Vortigaunt;

[RegisterComponent]
[ComponentProtoName("Vortigaunt")]
public sealed partial class VortigauntComponent : Component
{
    [DataField]
    public float LightningDamage = 18f;

    [DataField]
    public int LightningChainCount = 3;

    [DataField]
    public float LightningChainRange = 4.5f;

    [DataField]
    public float HealAmount = 30f;

    [DataField]
    public float HealChannelTime = 2.5f;

    [DataField]
    public float StunWaveRange = 5f;

    [DataField]
    public float StunDuration = 2.5f;

    [DataField]
    public float StunWaveDamage = 8f;

    [DataField]
    public EntProtoId LightningAction = "ActionVortigauntLightning";

    [DataField]
    public EntProtoId HealAction = "ActionVortigauntHeal";

    [DataField]
    public EntProtoId StunWaveAction = "ActionVortigauntStunWave";

    [ViewVariables]
    public EntityUid? LightningActionUid;

    [ViewVariables]
    public EntityUid? HealActionUid;

    [ViewVariables]
    public EntityUid? StunWaveActionUid;
}
public sealed partial class VortigauntLightningEvent : WorldTargetActionEvent
{
    [DataField]
    public float Damage = 18f;

    [DataField]
    public int ChainCount = 3;

    [DataField]
    public float ChainRange = 4.5f;
}
public sealed partial class VortigauntHealEvent : InstantActionEvent
{
    [DataField]
    public float HealAmount = 30f;

    [DataField]
    public float ChannelTime = 2.5f;
}
public sealed partial class VortigauntStunWaveEvent : InstantActionEvent
{
    [DataField]
    public float Range = 5f;

    [DataField]
    public float StunDuration = 2.5f;

    [DataField]
    public float Damage = 8f;
}

[Serializable, NetSerializable]
public sealed partial class VortigauntHealDoAfterEvent : SimpleDoAfterEvent { }
