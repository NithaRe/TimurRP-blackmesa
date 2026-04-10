using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Evac;

[Serializable, NetSerializable]
public enum EvacConsoleUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class EvacConsoleBoundUserInterfaceState : BoundUserInterfaceState
{
    public EvacConsoleState State;
    public TimeSpan? TargetTime;
    public bool PortalReady;

    public EvacConsoleBoundUserInterfaceState(EvacConsoleState state, TimeSpan? targetTime, bool portalReady)
    {
        State = state;
        TargetTime = targetTime;
        PortalReady = portalReady;
    }
}

[Serializable, NetSerializable]
public sealed class EvacConsoleOpenMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class EvacConsoleCloseMessage : BoundUserInterfaceMessage { }