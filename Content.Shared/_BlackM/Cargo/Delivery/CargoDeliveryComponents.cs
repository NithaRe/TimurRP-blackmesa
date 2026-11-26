using Content.Shared.Access;
using Content.Shared.Radio;
using Content.Shared.Roles;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Cargo.Delivery;

[RegisterComponent]
public sealed partial class CargoDeliveryTerminalComponent : Component
{
    [DataField]
    public float PadSearchRange = 8f;

    [DataField]
    public float DroneSearchRange = 60f;

    [DataField]
    public int Reward = 1000;

    [DataField]
    public int Penalty = 500;

    [DataField]
    public int CratePrice = 50;

    [DataField]
    public string Account = "Cargo";

    [DataField]
    public ProtoId<RadioChannelPrototype> RadioChannel = "Supply";

    [DataField]
    public EntProtoId CrateProto = "CrateCargoDelivery";

    [DataField]
    public float MinPointDistance = 12f;

    [DataField]
    public TimeSpan PointCooldown = TimeSpan.FromMinutes(2);

    [DataField]
    public TimeSpan CratePrintCooldown = TimeSpan.FromSeconds(20);

    [DataField]
    public EntityWhitelist? CrateWhitelist;

    [DataField]
    public EntityWhitelist? ContentsBlacklist;

    [DataField]
    public SoundSpecifier ConfirmSound = new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");

    [DataField]
    public SoundSpecifier ErrorSound = new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg");

    [ViewVariables]
    public TimeSpan NextCratePrint;

    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> PointCooldowns = new();

    [ViewVariables]
    public HashSet<EntityUid> ActiveDrones = new();

    [ViewVariables]
    public int TotalDelivered;

    [ViewVariables]
    public int TotalFailed;

    [ViewVariables]
    public float UiAccumulator;
}

[RegisterComponent]
public sealed partial class CargoDeliveryPadComponent : Component
{
    [DataField]
    public float CrateSearchRange = 0.45f;
}

[RegisterComponent]
public sealed partial class CargoDeliveryPointComponent : Component
{
    [DataField(required: true)]
    public ProtoId<DepartmentPrototype> Department;

    [DataField]
    public ProtoId<RadioChannelPrototype>? RadioChannel;

    [DataField]
    public List<ProtoId<AccessLevelPrototype>> Access = new();

    [ViewVariables]
    public int Incoming;
}

[RegisterComponent]
public sealed partial class CargoDeliveryCrateComponent : Component
{
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CargoDeliveryLockComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<ProtoId<AccessLevelPrototype>> Access = new();

    [DataField, AutoNetworkedField]
    public string DestinationName = string.Empty;
}

[Serializable, NetSerializable]
public enum CargoDeliveryDroneState : byte
{
    Idle,
    ToPad,
    Loading,
    Delivering,
    Downed,
    Returning,
}

[RegisterComponent]
public sealed partial class CargoDeliveryDroneComponent : Component
{
    [DataField]
    public float Speed = 4.5f;

    [DataField]
    public float ReturnSpeed = 6f;

    [DataField]
    public float WanderSpeed = 1.3f;

    [DataField]
    public float WanderRadius = 3.5f;

    [DataField]
    public float WanderPauseMin = 2f;

    [DataField]
    public float WanderPauseMax = 6f;

    [DataField]
    public float DownThreshold = 40f;

    [DataField]
    public float DownedDuration = 120f;

    [DataField]
    public float LoadingTime = 1.5f;

    [DataField]
    public float MaxFlightTime = 300f;

    [DataField]
    public float DeliverRange = 0.6f;

    [DataField]
    public float ArriveRange = 0.5f;

    [DataField]
    public string ContainerId = "cargo_delivery_crate";

    [ViewVariables]
    public CargoDeliveryDroneState State = CargoDeliveryDroneState.Idle;

    [ViewVariables]
    public EntityCoordinates? Home;

    [ViewVariables]
    public MapCoordinates? WanderTarget;

    [ViewVariables]
    public float WanderTimer;

    [ViewVariables]
    public float StateTimer;

    [ViewVariables]
    public float FlightTime;

    [ViewVariables]
    public EntityUid? Terminal;

    [ViewVariables]
    public EntityUid? Pad;

    [ViewVariables]
    public EntityUid? PendingCrate;

    [ViewVariables]
    public EntityUid? Crate;

    [ViewVariables]
    public EntityUid? Point;

    [ViewVariables]
    public string DestinationName = string.Empty;

    [ViewVariables]
    public MapCoordinates? LastSafeCoordinates;

    [ViewVariables]
    public bool Penalized;

    [ViewVariables]
    public float SafeCheckAccumulator;

    [ViewVariables]
    public Angle LastRotation;

    [DataField]
    public SoundSpecifier? DeliverSound = new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");

    [DataField]
    public SoundSpecifier? DownSound = new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_two.ogg");

    [DataField]
    public SoundSpecifier? RebootSound = new SoundPathSpecifier("/Audio/Effects/Cargo/beep.ogg");
}
