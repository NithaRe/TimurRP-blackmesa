using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._BlackM.Ams;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AmsComponent : Component
{
    [AutoNetworkedField]
    public AmsStatus Status = AmsStatus.Inactive;

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

    [AutoNetworkedField]
    public bool HasBeenUsed = false;

    [DataField]
    public float HecuSpawnDelay = 20f;

    public bool HecuSpawned = false;

    [DataField]
    public HashSet<string> PartSlots = new()
    {
        "ams_part_core",
        "ams_part_coil",
        "ams_part_lens",
        "ams_part_cell",
        "ams_part_stabilizer",
        "ams_part_casing",
    };

    [AutoNetworkedField]
    public HashSet<string> FilledSlots = new();

    [DataField]
    public float CalibrationDuration = 6f;

    [AutoNetworkedField]
    public Dictionary<string, TimeSpan> CalibrationStartTimes = new();

    [AutoNetworkedField]
    public HashSet<string> CalibratedSlots = new();

    public bool AllPartsInserted =>
        FilledSlots.IsSupersetOf(PartSlots) && CalibratedSlots.IsSupersetOf(PartSlots);
}
