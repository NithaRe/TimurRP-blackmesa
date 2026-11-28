using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._BlackM.ZenRift;

[RegisterComponent, Access(typeof(ZenRiftRuleSystem))]
public sealed partial class ZenRiftRuleComponent : Component
{
    [DataField]
    public EntProtoId RiftPrototype = "ZenRiftBlackM";

    [DataField]
    public int MinRifts = 1;

    [DataField]
    public int MaxRifts = 2;

    [DataField]
    public int MinMobsPerRift = 4;

    [DataField]
    public int MaxMobsPerRift = 7;

    [DataField]
    public TimeSpan? OpenDelay;

    [DataField]
    public TimeSpan OpenStagger = TimeSpan.FromSeconds(20);

    [DataField]
    public float MinRiftDistance = 15f;

    [DataField]
    public float MinPlayerDistance = 4f;

    [DataField]
    public int MaxPlacementAttempts = 60;

    [DataField]
    public string? TargetStationName;

    [DataField]
    public TimeSpan SweepDuration = TimeSpan.FromMinutes(4);

    [DataField]
    public TimeSpan MaxDuration = TimeSpan.FromMinutes(18);


    [DataField]
    public int WaveReward = 5000;

    [DataField]
    public int KillBonus = 400;

    [DataField]
    public int ClearBonus = 3000;

    [DataField]
    public int MaxReward;

    [DataField]
    public ProtoId<CargoAccountPrototype> RewardAccount = "Cargo";

    [DataField]
    public bool PayOnForcedEnd = true;


    [DataField]
    public LocId Sender = "zen-rift-sender";

    [DataField]
    public LocId StartAnnouncement = "zen-rift-announce-start";

    [DataField]
    public LocId BreachAnnouncement = "zen-rift-announce-breach";

    [DataField]
    public LocId CalmAnnouncement = "zen-rift-announce-calm";

    [DataField]
    public LocId ClearedAnnouncement = "zen-rift-announce-cleared";

    [DataField]
    public LocId SurvivedAnnouncement = "zen-rift-announce-survived";

    [DataField]
    public bool AnnounceLocations = true;

    [DataField]
    public SoundSpecifier? StartSound = new SoundPathSpecifier("/Audio/_BlackM/Announcements/ZenRift/zen_rift_start.ogg");

    [DataField]
    public SoundSpecifier? BreachSound = new SoundPathSpecifier("/Audio/_BlackM/Announcements/ZenRift/zen_rift_breach.ogg");

    [DataField]
    public SoundSpecifier? CalmSound = new SoundPathSpecifier("/Audio/_BlackM/Announcements/ZenRift/zen_rift_calm.ogg");

    [DataField]
    public SoundSpecifier? ClearedSound = new SoundPathSpecifier("/Audio/_BlackM/Announcements/ZenRift/zen_rift_cleared.ogg");

    [DataField]
    public SoundSpecifier? SurvivedSound = new SoundPathSpecifier("/Audio/_BlackM/Announcements/ZenRift/zen_rift_survived.ogg");

    [DataField]
    public Color InfoColor = Color.FromHex("#E8D44D");

    [DataField]
    public Color BreachColor = Color.FromHex("#5DFF3A");

    [DataField]
    public Color ResultColor = Color.FromHex("#5CC86B");


    [ViewVariables]
    public EntityUid? Station;

    [ViewVariables]
    public HashSet<EntityUid> Rifts = new();

    [ViewVariables]
    public HashSet<EntityUid> Mobs = new();

    [ViewVariables]
    public int TotalSpawned;

    [ViewVariables]
    public int Neutralized;

    [ViewVariables]
    public bool Placed;

    [ViewVariables]
    public TimeSpan MaxEndAt;

    [ViewVariables]
    public TimeSpan? SweepEndAt;

    [ViewVariables]
    public HashSet<string> Announced = new();

    [ViewVariables]
    public bool Finished;
}
