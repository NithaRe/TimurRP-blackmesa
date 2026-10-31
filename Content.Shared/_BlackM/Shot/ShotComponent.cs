namespace Content.Shared._BlackM.Shot;

[RegisterComponent]
public sealed partial class ShotComponent : Component
{
    [DataField]
    public Color TracerColor = Color.FromHex("#ff9a2e");

    [DataField]
    public Color CoreColor = Color.FromHex("#fff3d0");

    [DataField]
    public float Length = 1.0f;

    [DataField]
    public float Width = 0.0625f;

    [DataField]
    public float TracerChance = 1f;

    [DataField]
    public bool Muzzle = true;

    [DataField]
    public float MuzzleScale = 1f;

    [DataField]
    public bool Impact = true;

    [DataField]
    public float ImpactScale = 1f;

    [DataField]
    public bool Light = true;

    [DataField]
    public bool Smoke = false;

    [DataField] public float GlowSize = 0f;
    [DataField] public bool Embers = false;
    [DataField] public bool SmokeTrail = false;
    [DataField] public float SmokeAlpha = 0f;
    [DataField] public float SmokeScale = 1f;
    [DataField] public bool Wake = false;
}
