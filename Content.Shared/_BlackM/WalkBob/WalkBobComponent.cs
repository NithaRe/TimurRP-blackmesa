using System.Numerics;
using Robust.Shared.Maths;

namespace Content.Shared._BlackM.WalkBob;

[RegisterComponent]
public sealed partial class WalkBobComponent : Component
{

    [DataField]
    public float MinSpeedThreshold = 0.05f;

    [DataField]
    public float ReturnLerpSpeed = 10f;

    [DataField]
    public string? TargetLayerKey;

    [DataField]
    public float PhasePerMeter = 2.4f;

    [DataField]
    public float WalkBobHeight = 0.055f;

    [DataField]
    public float WalkSway = 0.8f;

    [DataField]
    public float WalkSquash = 0.012f;

    [DataField]
    public float? SprintSpeedThreshold;

    [DataField]
    public float RunBobHeight = 0.03f;

    [DataField]
    public float RunSway = 1.5f;

    [DataField]
    public float RunSquash = 0.028f;

    [DataField]
    public float RunStrideMult = 1.15f;

    [DataField]
    public float PullStrideMult = 0.75f;

    [DataField]
    public float RecoilImpulse = 2f;

    [DataField]
    public float RecoilStiffness = 220f;

    [DataField]
    public float RecoilDamping = 18f;

    [DataField]
    public float RecoilTilt = 35f;

    [DataField]
    public float RecoilSquash = 0.5f;

    [DataField]
    public float BreathAmplitude = 0.003f;

    [DataField]
    public float BreathSpeed = 2.2f;

    public float? VisualSpeedOverride;

    public float Phase;
    public float IdleTime;

    public float ActiveWeight;
    public float MoveWeight;
    public float RunWeight;
    public float PullWeight;

    public Vector2 RecoilOffset;
    public Vector2 RecoilVelocity;
    public TimeSpan NextKickTime;

    public Vector2 AppliedScale = Vector2.One;
    public Vector2 AppliedOffset;
    public Angle AppliedRotation = Angle.Zero;
    public bool AppliedOnLayer;
}