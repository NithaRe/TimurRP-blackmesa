using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Underground;

[Serializable, NetSerializable]
public enum UndergroundConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum UndergroundTabletUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class UndergroundTabletEarlyReturnMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public enum UndergroundConsolePhase : byte
{
    Offline,
    Idle,
    Generating,
    Ready,
    Outbound,
    Exploring,
    Returning,
    Cleanup,
    Faulted,
    Boarding,
}

[Serializable, NetSerializable]
public sealed class UndergroundConsoleState(
    UndergroundConsolePhase phase, int remainingSeconds, int durationSeconds, bool canStart, bool canReturn, UndergroundMission mission, int progress, bool canSelect, bool canClaim)
    : BoundUserInterfaceState
{
    public readonly UndergroundConsolePhase Phase = phase;
    public readonly int RemainingSeconds = remainingSeconds;
    public readonly int DurationSeconds = durationSeconds;
    public readonly bool CanStart = canStart;
    public readonly bool CanReturn = canReturn;
    public readonly UndergroundMission Mission = mission;
    public readonly int Progress = progress;
    public readonly bool CanSelect = canSelect;
    public readonly bool CanClaim = canClaim;
}

[Serializable, NetSerializable]
public sealed class UndergroundConsoleStartMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class UndergroundConsoleReturnMessage : BoundUserInterfaceMessage;
