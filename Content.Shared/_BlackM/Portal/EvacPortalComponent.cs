using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._BlackM.Portal;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EvacPortalComponent : Component
{
    [AutoNetworkedField]
    public EvacPortalStatus Status = EvacPortalStatus.Inactive;

    [AutoNetworkedField]
    public float EnergyCharge = 0f;

    [DataField]
    public float ChargeTime = 900f;

    [DataField]
    public float ActiveDuration = 300f;

    [AutoNetworkedField]
    public TimeSpan? SyncStartTime;

    [AutoNetworkedField]
    public TimeSpan? ActiveEndTime;

    public bool ClosingWarningSent = false;

    /// <summary>
    /// portal done
    /// </summary>
    [AutoNetworkedField]
    public bool HasBeenUsed = false;

    /// <summary>
    /// hecu time spawn
    /// </summary>
    [DataField]
    public float HecuSpawnDelay = 20f;

    /// <summary>
    /// hecu spawned?
    /// </summary>
    public bool HecuSpawned = false;
}