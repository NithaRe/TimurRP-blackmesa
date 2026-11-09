using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._BlackM.Turret;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class HecuTurretComponent : Component
{
    [DataField]
    public float Range = 9f;

    [DataField]
    public float RotationSpeed = 270f;

    [DataField]
    public float IdleRotateSpeed = 45f;

    [DataField]
    public float FireTolerance = 8f;

    [DataField]
    public TimeSpan PrepareTime = TimeSpan.FromSeconds(0.6);

    [DataField]
    public TimeSpan DisarmDelay = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.1);

    [DataField]
    public TimeSpan ScanInterval = TimeSpan.FromSeconds(0.3);

    [DataField]
    public bool AttackCritical;

    [DataField]
    public bool RequireAnchored = true;

    [DataField]
    public List<ProtoId<TagPrototype>> IgnoreTags = new() { "HECU" };

    [DataField]
    public SoundSpecifier? PrepareSound = new SoundPathSpecifier("/Audio/Effects/double_beep.ogg");

    [DataField]
    public SoundSpecifier? FoldSound;

    [DataField]
    public SoundSpecifier? IdleLoopSound = new SoundPathSpecifier("/Audio/_BlackM/CityScanner/scan_loop1.ogg");

    [DataField]
    public SoundSpecifier? ActiveLoopSound = new SoundPathSpecifier("/Audio/_BlackM/CityScanner/scan_loop2.ogg");

    [DataField]
    public EntProtoId? FoldedPrototype = "BlackMHecuLaserTurretFolded";

    [DataField]
    public TimeSpan FoldTime = TimeSpan.FromSeconds(2.5);

    [DataField]
    public SoundSpecifier? FoldUpSound = new SoundPathSpecifier("/Audio/Items/ratchet.ogg");

    [DataField]
    public string IdleState = "idle";

    [DataField]
    public string PrepareStateName = "prepare";

    [DataField]
    public string ArmedState = "armed";

    [DataField]
    public string FireState = "fire";

    // Networked runtime state
    [ViewVariables, AutoNetworkedField]
    public HecuTurretState State = HecuTurretState.Idle;

    [ViewVariables, AutoNetworkedField]
    public EntityUid? Target;

    [ViewVariables, AutoNetworkedField]
    public Angle Rotation = Angle.Zero;

    [ViewVariables, AutoNetworkedField]
    public int ShotCount;

    // Server-only runtime
    [ViewVariables]
    public TimeSpan PrepareEnd;

    [ViewVariables]
    public TimeSpan LastSeen;

    [ViewVariables]
    public TimeSpan NextUpdate;

    [ViewVariables]
    public TimeSpan NextScan;

    [ViewVariables]
    public bool? AmbienceOn;

    // Client-only runtime
    [ViewVariables]
    public Angle? VisualRotation;

    [ViewVariables]
    public HecuTurretState? LastVisualState;

    [ViewVariables]
    public int? LastShotCount;
}
