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
public sealed class AmsLeverMessage(string slot) : BoundUserInterfaceMessage
{
    public readonly string Slot = slot;
}

[Serializable, NetSerializable]
public sealed class AmsPartUiData(string slot, bool installed, string? material, int done, int needed, bool lever)
{
    public readonly string Slot = slot;
    public readonly bool Installed = installed;

    public readonly string? Material = material;

    public readonly int Done = done;
    public readonly int Needed = needed;
    public readonly bool Lever = lever;

    public bool Secured => Installed && Done >= Needed;
}

[Serializable, NetSerializable]
public sealed class AmsBuiState(
    AmsStatus status, TimeSpan? syncStart, TimeSpan? activeEnd, TimeSpan? detonation,
    float chargeTime, List<AmsPartUiData> parts, bool requireSample, bool sampleInPlace, bool primed)
    : BoundUserInterfaceState
{
    public readonly AmsStatus Status = status;
    public readonly TimeSpan? SyncStartTime = syncStart;
    public readonly TimeSpan? ActiveEndTime = activeEnd;
    public readonly TimeSpan? DetonationTime = detonation;
    public readonly float ChargeTime = chargeTime;
    public readonly List<AmsPartUiData> Parts = parts;
    public readonly bool RequireSample = requireSample;
    public readonly bool SampleInPlace = sampleInPlace;
    public readonly bool Primed = primed;
}
