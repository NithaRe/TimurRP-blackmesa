using System.Numerics;
using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Monorail;

public enum MonorailTrackShape : byte
{
    Straight,

    StraightLong,

    Corner,

    CurveLarge,

    End,
}

[RegisterComponent, NetworkedComponent]
public sealed partial class MonorailTrackComponent : Component
{
    [DataField]
    public MonorailTrackShape Shape = MonorailTrackShape.Straight;

    [DataField]
    public float SnapRange = 0.6f;

    [DataField]
    public float SnapFixRange = 0.3f;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class MonorailStopComponent : Component
{
    [DataField]
    public string Line = "main";

    [DataField]
    public string? StopName;
}

[DataDefinition]
public sealed partial class MonorailSeatEntry
{
    [DataField(required: true)]
    public Vector2 Offset;

    [DataField]
    public float Rotation;
}

public enum MonorailCarState : byte
{
    Uninitialized,
    Dwelling,
    Moving,
}

[Serializable, NetSerializable]
public enum MonorailCarVisuals : byte
{
    DoorsOpen,
}

[Serializable, NetSerializable]
public enum MonorailCarLayers : byte
{
    Doors,
    Lights,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MonorailCarComponent : Component
{
    #region Config

    [DataField]
    public string Line = "main";

    [DataField]
    public float InteriorDarkness = 0.5f;

    [DataField]
    public float InteriorFeather = 2f;

    [DataField]
    public float InteriorCornerRadius = 0.35f;

    [DataField]
    public Color InteriorTint = Color.FromHex("#05080f");

    [DataField]
    public float InteriorFadeTime = 0.6f;

    [DataField]
    public float MaxSpeed = 3.5f;

    [DataField]
    public float Acceleration = 1.1f;

    [DataField]
    public float DwellTime = 20f;

    [DataField]
    public float DepartureWarning = 6f;

    [DataField]
    public float DoorCloseTime = 2f;

    [DataField]
    public float BogieSpacing = 4.6f;

    [DataField]
    public Vector2 FloorHalfExtents = new(1.05f, 3.05f);

    [DataField]
    public float SearchRange = 4f;

    [DataField]
    public float ConnectTolerance = 0.3f;

    [DataField]
    public EntProtoId SeatPrototype = "BlackMMonorailSeat";

    [DataField]
    public List<MonorailSeatEntry> Seats = new();

    [DataField]
    public bool LockPassengersWhileMoving = true;

    [DataField]
    public bool LockStandingWhileMoving = true;

    [DataField]
    public bool CarryItems = true;

    [DataField]
    public SoundSpecifier? DepartureWarningSound;

    [DataField]
    public SoundSpecifier? DoorsCloseSound;

    [DataField]
    public SoundSpecifier? DoorsOpenSound;

    [DataField]
    public SoundSpecifier? DepartSound;

    [DataField]
    public SoundSpecifier? ArriveSound;

    [DataField]
    public Vector2 BodyHalfExtents = new(1.5f, 3.4f);

    [DataField]
    public float WallThickness = 0.35f;

    [DataField]
    public float DoorHalfWidth = 0.75f;

    [DataField]
    public List<string> DoorFixtures = new() { "doorLeft", "doorRight" };

    [DataField]
    public bool HitEnabled = true;

    [DataField]
    public float HitMinSpeed = 1f;

    [DataField]
    public float HitLookahead = 0.15f;

    [DataField]
    public float HitLookaheadTime = 0.08f;

    [DataField]
    public DamageSpecifier? HitDamage;

    [DataField]
    public float HitKnockdownTime = 4f;

    [DataField]
    public float HitThrowSpeed = 9f;

    [DataField]
    public float HitCooldown = 1.5f;

    [DataField]
    public SoundSpecifier? HitSound;

    [DataField]
    public SoundSpecifier? HornSound;

    [DataField]
    public float HornRange = 5f;

    [DataField]
    public float HornCooldown = 4f;

    [DataField]
    public EntProtoId? HeadlightPrototype = "BlackMMonorailHeadlight";

    [DataField]
    public float HeadlightInset = 0.1f;

    [DataField]
    public float HeadlightAngleOffset;

    [DataField]
    public Color HeadlightColor = Color.FromHex("#fff4e0");

    [DataField]
    public float HeadlightEnergy = 2.2f;

    [DataField]
    public Color TailLightColor = Color.FromHex("#ff3b2e");

    [DataField]
    public float TailLightEnergy = 0.7f;

    #endregion

    [DataField, AutoNetworkedField]
    public bool Moving;

    #region Runtime

    [ViewVariables] public MonorailCarState State = MonorailCarState.Uninitialized;
    [ViewVariables] public bool RouteDirty = true;
    [ViewVariables] public TimeSpan NextRetry;
    [ViewVariables] public string? LastError;

    [ViewVariables] public EntityUid Frame;

    [ViewVariables] public List<Vector2> Points = new();
    [ViewVariables] public List<float> Cumulative = new();
    [ViewVariables] public float Length;
    [ViewVariables] public bool Loop;

    [ViewVariables] public List<float> Stops = new();
    [ViewVariables] public List<string?> StopNames = new();
    [ViewVariables] public List<EntityUid> StopEntities = new();
    [ViewVariables] public int StopIndex;
    [ViewVariables] public int Direction = 1;

    [ViewVariables] public float Position;

    [ViewVariables] public Vector2 FramePosition;

    [ViewVariables] public float FrameRotation;

    [ViewVariables] public float TripFrom;
    [ViewVariables] public float TripTo;
    [ViewVariables] public TimeSpan TripStart;
    [ViewVariables] public float TripDuration;

    [ViewVariables] public TimeSpan NextDeparture;
    [ViewVariables] public bool WarningGiven;
    [ViewVariables] public bool DoorsClosed;

    [ViewVariables] public TimeSpan NextRiderScan;

    [ViewVariables] public float Speed;

    [ViewVariables] public TimeSpan NextHorn;

    [ViewVariables] public EntityUid? HeadlightNorth;

    [ViewVariables] public EntityUid? HeadlightSouth;

    #endregion
}

[RegisterComponent, NetworkedComponent]
public sealed partial class MonorailSeatComponent : Component
{
}
