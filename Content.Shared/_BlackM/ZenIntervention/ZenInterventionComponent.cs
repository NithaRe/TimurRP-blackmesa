using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._BlackM.ZenIntervention;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ZenInterventionComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Level;

    [DataField, AutoNetworkedField]
    public float MaxLevel = 100f;

    [DataField, AutoNetworkedField]
    public float GainPerMinute = 1f;

    [DataField, AutoNetworkedField]
    public float BreachThreshold = 60f;

    [DataField, AutoNetworkedField]
    public bool BreachTriggered;

    [DataField, AutoNetworkedField]
    public List<EntProtoId> BreachMobPrototypes = new()
    {
        "MobHeadcrabBlackM",
        "MobHoundeyeBlackM",
        "MobVortigauntBlackM",
    };

    [DataField, AutoNetworkedField]
    public int BreachSpawnAmount = 6;

    [DataField, AutoNetworkedField]
    public bool WaveActive;

    [DataField, AutoNetworkedField]
    public TimeSpan NextWaveTime = TimeSpan.Zero;

    [DataField, AutoNetworkedField]
    public TimeSpan WaveInterval = TimeSpan.FromMinutes(5);

    [DataField, AutoNetworkedField]
    public int WaveSpawnAmount = 2;

    [DataField, AutoNetworkedField]
    public EntProtoId WaveMobPrototype = "MobXenInfected";

    [DataField]
    public int MaxPlacementAttempts = 20;

    [DataField]
    public SoundSpecifier BreachSound = new SoundPathSpecifier("/Audio/Announcements/attention.ogg");

    [DataField]
    public SoundSpecifier WaveSound = new SoundPathSpecifier("/Audio/Announcements/attention.ogg");

    [DataField]
    public Color? AnnouncementColor;

    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [DataField]
    public string TargetStationName = "Комплекс Черная Меза";
}
