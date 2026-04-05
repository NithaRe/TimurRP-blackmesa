using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared.Actions;
using Content.Shared.DoAfter;

namespace Content.Shared._BlackM.Headcrab;

[RegisterComponent, NetworkedComponent]
[ComponentProtoName("Headcrab")]
public sealed partial class HeadcrabComponent : Component
{
    [DataField]
    public EntProtoId LeapAction = "ActionHeadcrabLeap";

    [DataField]
    public EntProtoId GrabAction = "ActionHeadcrabGrab";

    [ViewVariables]
    public EntityUid? LeapActionUid;

    [ViewVariables]
    public EntityUid? GrabActionUid;

    [DataField]
    public Dictionary<EntityUid, int> LeapHits = new();

    public int LeapsToKnockdown = 4;
}

[RegisterComponent]
public sealed partial class HeadcrabCapturedComponent : Component
{
    [DataField]
    public float SpeedModifier = 0.6f;
}

[RegisterComponent]
public sealed partial class HeadcrabLeapingComponent : Component
{
    [DataField]
    public float StaminaDamage = 20f;
}

public sealed partial class HeadcrabLeapEvent : WorldTargetActionEvent
{
    [DataField]
    public float Distance = 5f;

    [DataField]
    public float Speed = 10f;

    [DataField]
    public float StaminaDamage = 20f;
}

public sealed partial class HeadcrabGrabEvent : WorldTargetActionEvent { }

[Serializable, NetSerializable]
public sealed partial class HeadcrabAttachDoAfterEvent : SimpleDoAfterEvent { }