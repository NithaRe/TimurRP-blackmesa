using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Vehicles;

public sealed partial class BMCarEngineActionEvent : InstantActionEvent;

public sealed partial class BMCarHornActionEvent : InstantActionEvent;

public sealed partial class BMCarSirenActionEvent : InstantActionEvent;

public sealed partial class BMCarLightsActionEvent : InstantActionEvent;

public sealed partial class BMCarControlModeActionEvent : InstantActionEvent;

public sealed partial class BMCarExitActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class BMCarEnterDoAfterEvent : DoAfterEvent
{
    public bool Driver;

    public BMCarEnterDoAfterEvent(bool driver)
    {
        Driver = driver;
    }

    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class BMCarPullOutDoAfterEvent : SimpleDoAfterEvent;
