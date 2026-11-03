using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._BlackM.Gib;

[RegisterComponent]
public sealed partial class GibComponent : Component
{
    [DataField]
    public float PartsLifetime = 3.5f;

    [DataField]
    public float FadeDuration = 1.2f;

    [DataField]
    public bool DeleteItems = true;

    [DataField]
    public bool SuppressBlood = true;

    [DataField]
    public bool LaunchParts = true;

    [DataField]
    public float PartsSpeed = 5f;

    [DataField]
    public float PartsSpin = 12f;

    [DataField]
    public bool BloodTrail = true;

    [DataField]
    public Color BloodColor = Color.FromHex("#7d0a0d");

    [DataField]
    public float BurstStrength = 1f;

    [DataField]
    public bool Chunks = true;

    [DataField]
    public bool Nova = true;

    [DataField]
    public bool ScreenEffect = true;

    [DataField]
    public List<EntProtoId> Splats = new();

    [DataField]
    public int SplatCount = 8;

    [DataField]
    public float SplatRadius = 1.5f;

    [DataField]
    public float SplatLifetime = 25f;

    [DataField]
    public SoundSpecifier? Sound;

    [DataField]
    public SoundSpecifier? DropsSound;

    [DataField]
    public float DropsDelay = 0.22f;

    [DataField]
    public float ShockRange = 3f;

    [DataField]
    public float ShockForce = 4f;

    [DataField]
    public float ShakeStrength = 2f;

    [DataField]
    public float ShakeRadius = 8f;

    [DataField] public float PartsScatter = 0.3f;
}
