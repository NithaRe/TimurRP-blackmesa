using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._BlackM.Houndeye;

[RegisterComponent]
[ComponentProtoName("Houndeye")]
public sealed partial class HoundeyeComponent : Component
{
    [DataField] public float ScreamRange = 5f;
    [DataField] public float SlowModifier = 0.4f;
    [DataField] public float SlowDuration = 4f;
    [DataField] public float ScreamDamage = 10f;
    [DataField] public float ChargeThrowStrength = 12f;
    [DataField] public float ChargeStaminaDamage = 30f;
    [DataField] public float ChargeSpeed = 14f;
    [DataField] public float ChargeDistance = 6f;

    [DataField] public EntProtoId ScreamAction = "ActionHoundeyeScream";
    [DataField] public EntProtoId ChargeAction = "ActionHoundeyeCharge";

    [ViewVariables] public EntityUid? ScreamActionUid;
    [ViewVariables] public EntityUid? ChargeActionUid;
    [ViewVariables] public bool IsCharging = false;
}

[RegisterComponent]
public sealed partial class HoundeyeSlowedComponent : Component
{
    [DataField] public float SlowModifier = 0.4f;
    [ViewVariables] public TimeSpan EndTime;
}

public sealed partial class HoundeyeScreamEvent : InstantActionEvent { }

public sealed partial class HoundeyeChargeEvent : WorldTargetActionEvent
{
    [DataField] public float Speed = 14f;
    [DataField] public float Distance = 6f;
    [DataField] public float StaminaDamage = 30f;
    [DataField] public float ThrowStrength = 12f;
}

[Serializable, NetSerializable]
public sealed partial class HoundeyeScreamDoAfterEvent : SimpleDoAfterEvent { }