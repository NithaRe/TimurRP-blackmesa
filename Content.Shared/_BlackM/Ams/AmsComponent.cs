using System;
using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Shared._BlackM.Ams;

[NetSerializable, Serializable]
public enum AmsStatus : byte
{
    Inactive,

    Synchronizing,

    Ready,

    Active,

    Detonating,

    Destroyed,
}

public enum AmsPartType : byte
{
    Core,
    Coil,
    FocusingLens,
    PowerCell,
    Stabilizer,
    Casing,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AmsComponent : Component
{
    [DataField, AutoNetworkedField]
    public AmsStatus Status = AmsStatus.Inactive;

    [DataField, AutoNetworkedField]
    public TimeSpan? SyncStartTime;

    [DataField, AutoNetworkedField]
    public TimeSpan? ActiveEndTime;

    [DataField, AutoNetworkedField]
    public TimeSpan? DetonationTime;

    [DataField, AutoNetworkedField]
    public float ChargeTime = 900f;

    [DataField]
    public float ActiveDuration = 300f;

    [DataField]
    public float DetonationDelay = 60f;

    [DataField]
    public float HecuSpawnDelay = 20f;

    [DataField]
    public List<string> PartSlots = new()
    {
        "ams_part_core",
        "ams_part_coil",
        "ams_part_lens",
        "ams_part_cell",
        "ams_part_stabilizer",
        "ams_part_casing",
    };

    [DataField]
    public int SpecialDoorRequiredParts = 4;

    [DataField]
    public float ZenReductionPerPart = 20f;

    [DataField]
    public List<string> InfectionDecals = new() { "AmsInfectedDecal1", "AmsInfectedDecal2" };

    [DataField]
    public float InfectionStartRate = 1.5f;

    [DataField]
    public float InfectionEndRate = 10f;

    [DataField] public string ExplosionType = "Default";
    [DataField] public float CoreIntensity = 60000f;
    [DataField] public float CoreSlope = 5f;
    [DataField] public float CoreMaxTileIntensity = 200f;

    [DataField] public int WaveCount = 14;
    [DataField] public float WaveInterval = 0.7f;
    [DataField] public int ExplosionsPerWave = 4;
    [DataField] public float WaveIntensity = 3500f;
    [DataField] public float WaveSlope = 4f;
    [DataField] public float WaveMaxTileIntensity = 90f;

    [DataField]
    public float RoundEndDelay = 30f;

    [ViewVariables] public bool HecuSpawned;
    [ViewVariables] public bool ClosingWarningSent;
    [ViewVariables] public bool SpecialDoorOpened;
    [ViewVariables] public TimeSpan NextUiUpdate;
    [ViewVariables] public TimeSpan NextFlicker;
    [ViewVariables] public EntityUid? RiftEffect;
}

[RegisterComponent]
public sealed partial class AmsPartComponent : Component
{
    [DataField(required: true)]
    public AmsPartType PartType;
}

[RegisterComponent]
public sealed partial class HecuSpawnPointComponent : Component
{
}

[RegisterComponent]
public sealed partial class AmsControlledDoorComponent : Component
{
    [DataField(required: true), ViewVariables(VVAccess.ReadWrite)]
    public string GroupId = string.Empty;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string AnnouncementLocationName = string.Empty;
}

public static class AmsHelpers
{
    public static float GetCharge(AmsStatus status, TimeSpan? syncStart, float chargeTime, TimeSpan now)
    {
        return status switch
        {
            AmsStatus.Synchronizing when syncStart is { } start && chargeTime > 0f
                => Math.Clamp((float) (now - start).TotalSeconds / chargeTime, 0f, 1f),
            AmsStatus.Ready or AmsStatus.Active or AmsStatus.Detonating or AmsStatus.Destroyed => 1f,
            _ => 0f,
        };
    }
}
