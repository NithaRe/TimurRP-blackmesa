using Content.Shared._BlackM.ZenRift;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._BlackM.ZenRift;

[RegisterComponent, Access(typeof(ZenRiftSystem), typeof(ZenRiftRuleSystem))]
public sealed partial class ZenRiftComponent : Component
{
    [DataField]
    public TimeSpan OpenDuration = TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(15);

    [DataField]
    public TimeSpan ActiveDuration = TimeSpan.FromMinutes(4);

    [DataField]
    public TimeSpan LingerDuration = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan CloseDuration = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan MinSpawnInterval = TimeSpan.FromSeconds(15);

    [DataField]
    public TimeSpan MaxSpawnInterval = TimeSpan.FromSeconds(30);

    [DataField]
    public int MinBatch = 1;

    [DataField]
    public int MaxBatch = 2;

    [DataField]
    public int SpawnBudget = 6;

    [DataField]
    public int MaxAlive = 4;

    [DataField]
    public Dictionary<EntProtoId, float> Mobs = new()
    {
        { "MobHeadcrabStationBlackM", 5f },
        { "MobHoundeyeStationBlackM", 3f },
        { "MobVortigauntStationBlackM", 1.5f },
    };

    [DataField]
    public int SpawnRadius = 1;

    [DataField]
    public EntProtoId? SpawnEffect = "AmsXenPortal";

    [DataField]
    public SoundSpecifier? OpenSound = new SoundPathSpecifier("/Audio/_BlackM/Ams/ams_portal.ogg");

    [DataField]
    public SoundSpecifier? SpawnSound = new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg");

    [DataField]
    public SoundSpecifier? CloseSound = new SoundPathSpecifier("/Audio/Effects/teleport_departure.ogg");

    [DataField]
    public Color OpeningColor = Color.FromHex("#A64DFF");

    [DataField]
    public Color ActiveColor = Color.FromHex("#C266FF");

    [DataField]
    public Color ClosingColor = Color.FromHex("#7A5C99");


    [ViewVariables]
    public EntityUid? Rule;

    [ViewVariables]
    public ZenRiftPhase Phase = ZenRiftPhase.Opening;

    [ViewVariables]
    public TimeSpan PhaseEnd;

    [ViewVariables]
    public TimeSpan NextSpawn;

    [ViewVariables]
    public int Spawned;

    [ViewVariables]
    public HashSet<EntityUid> AliveMobs = new();

    [ViewVariables]
    public EntityUid? Grid;
}

[RegisterComponent]
public sealed partial class ZenRiftSpawnedComponent : Component
{
    [ViewVariables]
    public EntityUid? Rift;

    [ViewVariables]
    public EntityUid? Rule;

    [ViewVariables]
    public bool Neutralized;
}

[ByRefEvent]
public readonly record struct ZenRiftClosedEvent(EntityUid Rift, int Spawned);

[ByRefEvent]
public readonly record struct ZenRiftMobSpawnedEvent(EntityUid Rift, EntityUid Mob);

[ByRefEvent]
public readonly record struct ZenRiftMobNeutralizedEvent(EntityUid Mob);

[ByRefEvent]
public readonly record struct ZenRiftActivatedEvent(EntityUid Rift);
