using System.Numerics;
using Robust.Shared.Maths;

namespace Content.Shared._BlackM.WalkBob;

[RegisterComponent]
public sealed partial class WalkBobComponent : Component
{
    [DataField]
    public string? TargetLayerKey;

    [DataField]
    public float FootOffset = 0.47f;

    [DataField]
    public float MinSpeedThreshold = 0.15f;

    [DataField]
    public float MaxAnimatedSpeed = 8f;

    [DataField]
    public float? SprintSpeedThreshold;

    [DataField]
    public float Smoothing = 8f;

    [DataField]
    public float MaxRotation = 6f;

    [DataField]
    public float PhasePerMeter = 2.2f;

    [DataField]
    public float StepShift = 0.022f;

    [DataField]
    public float RunShiftMult = 1.3f;

    [DataField]
    public float BreathAmplitude = 0.02f;

    [DataField]
    public float BreathRate = 1.6f;

    [DataField]
    public float BreathAmplitudeExerted = 0.032f;

    [DataField]
    public float BreathRateExerted = 3.6f;

    [DataField]
    public float ExertionBuildTime = 6f;

    [DataField]
    public float ExertionRecoverTime = 6f;

    [DataField]
    public float RecoilKick = 0.08f;

    [DataField]
    public float RecoilMax = 0.14f;

    [DataField]
    public float RecoilTilt = 3.5f;

    [DataField]
    public float RecoilReturn = 6f;

    [DataField]
    public float ReachAngle = 3f;

    [DataField]
    public float ReachDip = 0.03f;

    [DataField]
    public float ReachShift = 0.03f;

    [DataField]
    public float ReachDuration = 0.3f;

    [DataField]
    public float ThrowAngle = 5f;

    [DataField]
    public float ThrowShift = 0.06f;

    [DataField]
    public float ThrowDuration = 0.4f;

    public float? VisualSpeedOverride;

    public float Phase;
    public float BreathPhase;
    public float Exertion;

    public float ActiveWeight = 1f;
    public float MoveWeight;
    public float VerticalWeight;
    public float RunWeight;

    public Vector2 RecoilOffset;
    public Vector2 RecoilVelocity;
    public float RecoilRotation;
    public float RecoilRotationVelocity;
    public TimeSpan NextKickTime;

    public float ReachTimeLeft;
    public float ThrowTimeLeft;
    public Vector2 ThrowDirection;
    public Vector2 ReachDirection;

    public Vector2 AppliedScale = Vector2.One;
    public Vector2 AppliedOffset;
    public Angle AppliedRotation = Angle.Zero;
    public bool AppliedOnLayer;

    public Vector2 WrittenScale = Vector2.One;
    public Vector2 WrittenOffset;
    public Angle WrittenRotation = Angle.Zero;
}
