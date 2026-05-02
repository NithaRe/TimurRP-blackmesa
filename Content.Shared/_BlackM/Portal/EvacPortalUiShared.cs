using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Portal;

[NetSerializable, Serializable]
public enum EvacPortalUiKey : byte
{
    Key,
}

/// <summary>
/// portal status
/// </summary>
[NetSerializable, Serializable]
public enum EvacPortalStatus : byte
{
    Inactive,
    Synchronizing,
    Ready,
    Active,
    Used,
}

/// <summary>
/// ui state
/// </summary>
[NetSerializable, Serializable]
public sealed class EvacPortalBuiState : BoundUserInterfaceState
{
    public EvacPortalStatus Status;
    public float EnergyCharge;     // 0.0 - 1.0
    public TimeSpan? CountdownEnd;

    public EvacPortalBuiState(EvacPortalStatus status, float energyCharge, TimeSpan? countdownEnd)
    {
        Status = status;
        EnergyCharge = energyCharge;
        CountdownEnd = countdownEnd;
    }
}

/// <summary>
/// client msg
/// </summary>
[NetSerializable, Serializable]
public sealed class EvacPortalLaunchMessage : BoundUserInterfaceMessage;

/// <summary>
/// client teleport
/// </summary>
[NetSerializable, Serializable]
public sealed class EvacPortalTeleportMessage : BoundUserInterfaceMessage;