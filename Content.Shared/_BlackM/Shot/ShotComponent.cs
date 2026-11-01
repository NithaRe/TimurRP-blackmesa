namespace Content.Shared._BlackM.Shot;

[RegisterComponent]
public sealed partial class ShotComponent : Component
{
    [DataField] public string Style = "fire";
    [DataField] public Color TracerColor = Color.FromHex("#ff9a2e");
    [DataField] public Color CoreColor = Color.FromHex("#fff3d0");

    [DataField] public bool Impact = true;
    [DataField] public float ImpactScale = 1f;
    [DataField] public bool Light = true;

    [DataField] public bool Smoke = true;
    [DataField] public bool SmokeTrail = true;
    [DataField] public float TrailChance = 0.6f;
    [DataField] public float TrailWidth = 0.22f;
    [DataField] public float TrailWave = 1f;
    [DataField] public float TrailLife = 1.3f;

    [DataField] public float SmokeAmount = 1f;
    [DataField] public float SmokeScale = 1f;
    [DataField] public float SmokeAlpha = 0.6f;
    [DataField] public Color SmokeColor = Color.FromHex("#c4c4c9");

    [DataField] public bool Tracer = false;
    [DataField] public float Length = 1.0f;
    [DataField] public float Width = 0.0625f;
    [DataField] public float TracerChance = 1f;
}
