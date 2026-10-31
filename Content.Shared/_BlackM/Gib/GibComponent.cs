using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._BlackM.Gib;

[RegisterComponent]
public sealed partial class GibComponent : Component
{
    [DataField]
    public float PartsLifetime = 3f;

    [DataField]
    public float FadeDuration = 1.2f;

    [DataField]
    public bool DeleteItems = true;

    [DataField]
    public bool LaunchParts = true;

    [DataField]
    public float PartsScatter = 0.3f;

    [DataField]
    public Color BloodColor = Color.FromHex("#7d0a0d");

    [DataField]
    public float BurstStrength = 1f;

    [DataField]
    public List<EntProtoId> Splats = new();

    [DataField]
    public int SplatCount = 6;

    [DataField]
    public float SplatRadius = 1.3f;

    [DataField]
    public float SplatLifetime = 20f;

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
}
