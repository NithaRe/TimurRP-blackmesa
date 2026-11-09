using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.CityScanner;

[RegisterComponent]
public sealed partial class CityScannerComponent : Component
{
    [DataField]
    public float SearchRange = 7f;

    [DataField]
    public float PhotoRange = 3.5f;

    [DataField]
    public TimeSpan AimTime = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan FlashTime = TimeSpan.FromSeconds(0.45);

    [DataField]
    public TimeSpan FlashDuration = TimeSpan.FromSeconds(1.5);

    [DataField]
    public TimeSpan MinCooldown = TimeSpan.FromSeconds(12);

    [DataField]
    public TimeSpan MaxCooldown = TimeSpan.FromSeconds(30);

    // The same person is ignored for this long after a photo.
    [DataField]
    public TimeSpan TargetCooldown = TimeSpan.FromSeconds(150);

    [DataField]
    public TimeSpan FollowTime = TimeSpan.FromSeconds(25);

    [DataField]
    public TimeSpan MinIdleSound = TimeSpan.FromSeconds(8);

    [DataField]
    public TimeSpan MaxIdleSound = TimeSpan.FromSeconds(20);

    [DataField]
    public EntProtoId PhotoPrototype = "BMScannerPhoto";

    [DataField]
    public EntProtoId FlashEffect = "BMCityScannerFlashEffect";

    [DataField]
    public SoundSpecifier AlertSound = new SoundPathSpecifier("/Audio/_BlackM/CityScanner/alert1.ogg",
        AudioParams.Default.WithVolume(-2f));

    [DataField]
    public SoundSpecifier AimSound = new SoundPathSpecifier("/Audio/_BlackM/CityScanner/scan_loop1.ogg",
        AudioParams.Default.WithVolume(-3f));

    [DataField]
    public SoundSpecifier ShutterSound = new SoundPathSpecifier("/Audio/_BlackM/CityScanner/photo1.ogg");

    [DataField]
    public SoundSpecifier IdleSound = new SoundCollectionSpecifier("BMCityScannerIdle",
        AudioParams.Default.WithVolume(-6f));

    [ViewVariables]
    public CityScannerState State = CityScannerState.Idle;

    [ViewVariables]
    public EntityUid? Target;

    [ViewVariables]
    public TimeSpan StateEnd;

    [ViewVariables]
    public TimeSpan NextShot;

    [ViewVariables]
    public TimeSpan NextSearch;

    [ViewVariables]
    public TimeSpan FollowEnd;

    [ViewVariables]
    public TimeSpan NextIdleSound;

    public readonly Dictionary<EntityUid, TimeSpan> Photographed = new();
}

[Serializable, NetSerializable]
public enum CityScannerVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum CityScannerState : byte
{
    Idle,
    Aiming,
    Flash,
}

public enum CityScannerLayers : byte
{
    Shadow,
    Body,
    Eye,
}
