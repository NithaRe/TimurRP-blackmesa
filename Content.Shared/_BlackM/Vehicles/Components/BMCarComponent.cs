using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._BlackM.Vehicles.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class BMCarComponent : Component
{
    #region Seats

    [DataField]
    public string DriverSlotId = "bm_car_driver";

    [DataField]
    public string PassengerContainerId = "bm_car_passengers";

    [DataField, AutoNetworkedField]
    public int PassengerSeats = 1;

    [ViewVariables]
    public ContainerSlot DriverSlot = default!;

    [ViewVariables]
    public Container Passengers = default!;

    [DataField]
    public TimeSpan EnterDelay = TimeSpan.FromSeconds(1.2);

    [DataField]
    public TimeSpan PutInDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan PullOutDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public float ExitDistance = 0.95f;

    #endregion

    #region Engine

    [DataField, AutoNetworkedField]
    public BMCarEngineState Engine = BMCarEngineState.Off;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan EngineReadyAt;

    [DataField]
    public TimeSpan IgnitionTime = TimeSpan.FromSeconds(1.4);

    [DataField, AutoNetworkedField]
    public bool Headlights;

    [DataField, AutoNetworkedField]
    public bool Siren;

    #endregion

    #region Driving

    [DataField, AutoNetworkedField]
    public float MaxSpeed = 8f;

    /// <summary>true: W/S throttle and A/D steer. false: screen-relative.</summary>
    [DataField, AutoNetworkedField]
    public bool TankControls = true;

    [DataField, AutoNetworkedField]
    public float ReverseSpeed = 3.5f;

    [DataField, AutoNetworkedField]
    public float Acceleration = 6.5f;

    [DataField, AutoNetworkedField]
    public float Braking = 14f;

    [DataField, AutoNetworkedField]
    public float Coast = 2.5f;

    [DataField, AutoNetworkedField]
    public float HandBrake = 7f;

    [DataField, AutoNetworkedField]
    public float TurnRate = 3.4f;

    [DataField, AutoNetworkedField]
    public float FullTurnSpeed = 1.5f;

    [DataField, AutoNetworkedField]
    public float PivotTurnRate = 1.5f;

    [DataField, AutoNetworkedField]
    public float HighSpeedSteerLoss = 0.25f;

    [DataField, AutoNetworkedField]
    public float Grip = 14f;

    [DataField, AutoNetworkedField]
    public float DriftGrip = 1.6f;

    [DataField, AutoNetworkedField]
    public float DriftTurnBoost = 1.6f;

    [DataField, AutoNetworkedField]
    public float TurnCarry = 1f;

    [DataField, AutoNetworkedField]
    public float DriftCarry = 0.3f;

    [DataField]
    public float MaxWheelAngle = 32f;

    [DataField, AutoNetworkedField]
    public float ReverseAngle = 115f;

    [DataField, AutoNetworkedField]
    public float BrakeAngle = 150f;

    [DataField]
    public float TireMarkLifetime = 8f;

    #endregion

    #region Impacts

    [DataField]
    public string BumperFixtureId = "bmcar_bumper";

    [DataField]
    public float MinRamSpeed = 2.2f;

    [DataField]
    public DamageSpecifier RamDamage = new();

    [DataField]
    public DamageSpecifier RunOverDamage = new();

    [DataField]
    public float RamKnockdown = 3f;

    [DataField]
    public float RamThrowFactor = 0.45f;

    [DataField]
    public float RamSlowdown = 0.75f;

    [DataField]
    public TimeSpan RamCooldown = TimeSpan.FromSeconds(1);

    [DataField]
    public float CrashSpeed = 4.5f;

    [DataField]
    public DamageSpecifier CrashDamage = new();

    [DataField]
    public DamageSpecifier OccupantCrashDamage = new();

    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> LastHit = new();

    [ViewVariables]
    public TimeSpan NextCrash;

    #endregion

    #region Sounds

    [DataField]
    public SoundSpecifier? StartSound = new SoundPathSpecifier("/Audio/Effects/Vehicle/vehiclestartup.ogg");

    [DataField]
    public SoundSpecifier? StopSound;

    [DataField]
    public SoundSpecifier EngineLoopSound = new SoundPathSpecifier("/Audio/Effects/Vehicle/vehicleengineidle.ogg");

    [DataField]
    public float EngineVolume;

    [DataField]
    public float EngineRange = 14f;

    [DataField]
    public SoundSpecifier? HornSound = new SoundPathSpecifier("/Audio/Effects/Vehicle/carhorn.ogg");

    [DataField]
    public SoundSpecifier? SirenSound = new SoundPathSpecifier("/Audio/Effects/Vehicle/policesiren.ogg");

    [DataField]
    public float SirenVolume = -3f;

    [DataField]
    public float SirenRange = 22f;

    [DataField]
    public SoundSpecifier? DoorSound = new SoundCollectionSpecifier("MetalThud", AudioParams.Default.WithVolume(-4f).WithVariation(0.1f));

    [DataField]
    public SoundSpecifier? LightsSound;

    [DataField]
    public SoundSpecifier? RamSound = new SoundCollectionSpecifier("MetalSlam", AudioParams.Default.WithVariation(0.1f));

    [DataField]
    public SoundSpecifier? RunOverSound = new SoundCollectionSpecifier("MetalCrunch", AudioParams.Default.WithVariation(0.1f));

    [DataField]
    public SoundSpecifier? CrashSound = new SoundCollectionSpecifier("MetalBreak", AudioParams.Default.WithVariation(0.1f));

    #endregion

    #region Actions

    [DataField]
    public EntProtoId? EngineAction = "ActionBMCarEngine";

    [DataField]
    public EntProtoId? HornAction = "ActionBMCarHorn";

    [DataField]
    public EntProtoId? SirenAction = "ActionBMCarSiren";

    [DataField]
    public EntProtoId? LightsAction = "ActionBMCarLights";

    [DataField]
    public EntProtoId? ControlModeAction = "ActionBMCarControlMode";

    [DataField]
    public EntProtoId? ExitAction = "ActionBMCarExit";

    [ViewVariables]
    public Dictionary<EntityUid, List<EntityUid>> OccupantActions = new();

    #endregion
}

[Serializable, NetSerializable]
public enum BMCarEngineState : byte
{
    Off,
    Starting,
    Running,
}
