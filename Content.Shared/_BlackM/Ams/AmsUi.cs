using System;
using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Ams;

[Serializable, NetSerializable]
public enum AmsUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class AmsLaunchButtonMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class AmsBuiState(
    AmsStatus status, TimeSpan? syncStart, TimeSpan? activeEnd, TimeSpan? detonation,
    float chargeTime, List<string> allSlots, List<string> filledSlots) : BoundUserInterfaceState
{
    public readonly AmsStatus Status = status;
    public readonly TimeSpan? SyncStartTime = syncStart;
    public readonly TimeSpan? ActiveEndTime = activeEnd;
    public readonly TimeSpan? DetonationTime = detonation;
    public readonly float ChargeTime = chargeTime;

    public readonly List<string> AllSlots = allSlots;

    public readonly List<string> FilledSlots = filledSlots;
}
