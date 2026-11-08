using System.Numerics;
using Content.Shared._BlackM.Vehicles.Components;
using Content.Shared.Friction;
using Content.Shared.Gravity;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Controllers;

namespace Content.Shared._BlackM.Vehicles.Systems;

public sealed class BMCarController : VirtualController
{
    [Dependency] private readonly SharedMoverController _mover = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedGravitySystem _gravity = default!;

    private const float StopEpsilonSq = 0.0025f;
    private const float MovingSpeed = 0.3f;

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(SharedMoverController));
        UpdatesAfter.Add(typeof(TileFrictionController));
        base.Initialize();
    }

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        base.UpdateBeforeSolve(prediction, frameTime);

        var query = EntityQueryEnumerator<BMCarComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var car, out var body, out var xform))
        {
            if (prediction && !body.Predict)
                continue;

            if (body.BodyType == BodyType.Static || !body.CanCollide)
                continue;

            Drive(uid, car, body, xform, frameTime);
        }
    }

    private void Drive(EntityUid uid, BMCarComponent car, PhysicsComponent body, TransformComponent xform, float frameTime)
    {
        var velocity = body.LinearVelocity;
        var driving = car.Engine == BMCarEngineState.Running && car.DriverSlot?.ContainedEntity != null;

        var buttons = MoveButtons.None;
        InputMoverComponent? mover = null;
        if (car.DriverSlot?.ContainedEntity != null && TryComp(uid, out mover) && mover.CanMove)
            buttons = mover.HeldMoveButtons;

        // Sprinting is inverted in this fork, so read the raw Walk bit.
        var handbrake = (buttons & MoveButtons.Walk) != 0;
        var move = SharedMoverController.GetNormalizedMovement(buttons) & MoveButtons.AnyDirection;
        if (!driving)
            move = MoveButtons.None;

        if (move == MoveButtons.None && velocity.LengthSquared() < StopEpsilonSq)
        {
            if (velocity != Vector2.Zero)
                PhysicsSystem.SetLinearVelocity(uid, Vector2.Zero, body: body);
            return;
        }

        if (_gravity.IsWeightless(uid, body, xform))
            return;

        var heading = _transform.GetWorldRotation(xform);
        var forward = heading.ToWorldVec();
        var speed = Vector2.Dot(velocity, forward);

        var target = 0f;
        var rate = driving ? car.Coast : car.HandBrake;
        var steer = 0.0;
        var steerIsAngle = true;
        var reversing = speed < -MovingSpeed;

        if (car.TankControls)
        {
            var throttle = ((move & MoveButtons.Up) != 0 ? 1 : 0) - ((move & MoveButtons.Down) != 0 ? 1 : 0);
            var wheel = ((move & MoveButtons.Left) != 0 ? 1 : 0) - ((move & MoveButtons.Right) != 0 ? 1 : 0);
            steerIsAngle = false;
            steer = wheel;

            if (throttle > 0)
            {
                if (speed < -MovingSpeed)
                {
                    rate = car.Braking;
                }
                else
                {
                    target = car.MaxSpeed;
                    rate = SpeedRate(car, speed, target);
                }
            }
            else if (throttle < 0)
            {
                if (speed > MovingSpeed)
                {
                    rate = car.Braking;
                }
                else
                {
                    target = -car.ReverseSpeed;
                    rate = SpeedRate(car, speed, target);
                    reversing = true;
                }
            }
        }
        else if (move != MoveButtons.None)
        {
            var local = ButtonsToVector(move);
            var input = Vector2.Normalize(_mover.GetParentGridAngle(mover!).RotateVec(local));
            var inputTheta = input.ToWorldAngle().Theta;
            var diff = AngleDiff(heading.Theta, inputTheta);
            var absDiff = Math.Abs(diff);
            var forwardLimit = DegToRad(speed > MovingSpeed ? car.BrakeAngle : car.ReverseAngle);

            if (absDiff <= forwardLimit)
            {
                if (speed < -MovingSpeed)
                {
                    rate = car.Braking;
                }
                else
                {
                    var sharpness = (float) Math.Clamp((absDiff - Math.PI / 6.0) / (Math.PI / 2.0), 0.0, 1.0);
                    target = car.MaxSpeed * (1f - 0.45f * sharpness);
                    steer = diff;
                    rate = SpeedRate(car, speed, target);
                }
            }
            else if (speed > MovingSpeed)
            {
                rate = car.Braking;
            }
            else
            {
                target = -car.ReverseSpeed;
                steer = AngleDiff(heading.Theta + Math.PI, inputTheta);
                rate = SpeedRate(car, speed, target);
                reversing = true;
            }
        }

        if (handbrake)
        {
            target = 0f;
            rate = car.HandBrake;
        }

        var turn = 0.0;
        if (steer != 0.0)
        {
            var absSpeed = Math.Abs(speed);

            var low = Math.Clamp(absSpeed / Math.Max(car.FullTurnSpeed, 0.01f), 0f, 1f);
            var turnRate = car.PivotTurnRate + (car.TurnRate - car.PivotTurnRate) * low;
            var high = Math.Clamp((absSpeed - car.FullTurnSpeed) / Math.Max(car.MaxSpeed - car.FullTurnSpeed, 0.1f), 0f, 1f);
            turnRate *= 1f - car.HighSpeedSteerLoss * high;
            if (handbrake)
                turnRate *= car.DriftTurnBoost;

            var maxTurn = (double) (Math.Max(turnRate, 0f) * frameTime);
            if (steerIsAngle)
            {
                turn = Math.Clamp(steer, -maxTurn, maxTurn);
            }
            else
            {
                turn = steer * maxTurn * (reversing ? -1.0 : 1.0);
            }
        }

        var carry = handbrake ? car.DriftCarry : car.TurnCarry;
        if (turn != 0.0)
            velocity = new Angle(turn * carry).RotateVec(velocity);

        var newHeading = new Angle(heading.Theta + turn);
        var newForward = newHeading.ToWorldVec();
        var newSpeed = Vector2.Dot(velocity, newForward);
        var lateral = velocity - newForward * newSpeed;

        newSpeed = MoveTowards(newSpeed, target, rate * frameTime);
        var grip = handbrake ? car.DriftGrip : car.Grip;
        lateral *= Math.Clamp(1f - grip * frameTime, 0f, 1f);

        var newVelocity = newForward * newSpeed + lateral;
        if (move == MoveButtons.None && newVelocity.LengthSquared() < StopEpsilonSq)
            newVelocity = Vector2.Zero;

        PhysicsSystem.SetLinearVelocity(uid, newVelocity, body: body);
        PhysicsSystem.SetAngularVelocity(uid, 0f, body: body);

        if (turn != 0.0)
            _transform.SetLocalRotation(uid, new Angle(xform.LocalRotation.Theta + turn), xform);
    }

    private static float SpeedRate(BMCarComponent car, float speed, float target)
    {
        if (speed * target < 0f)
            return car.Braking;

        if (Math.Abs(target) < Math.Abs(speed))
            return car.Coast * 2f;

        var load = Math.Clamp(Math.Abs(speed) / Math.Max(car.MaxSpeed, 0.1f), 0f, 1f);
        var accel = target < 0f ? car.Acceleration * 0.75f : car.Acceleration;
        return accel * (1f - 0.45f * load);
    }

    private static float MoveTowards(float current, float target, float maxDelta)
    {
        if (Math.Abs(target - current) <= maxDelta)
            return target;

        return current + Math.Sign(target - current) * maxDelta;
    }

    private static double AngleDiff(double from, double to)
    {
        var d = (to - from) % (Math.PI * 2.0);
        if (d > Math.PI)
            d -= Math.PI * 2.0;
        else if (d < -Math.PI)
            d += Math.PI * 2.0;
        return d;
    }

    private static double DegToRad(float degrees) => degrees * Math.PI / 180.0;

    private static Vector2 ButtonsToVector(MoveButtons buttons)
    {
        var vec = Vector2.Zero;
        if ((buttons & MoveButtons.Up) != 0)
            vec.Y += 1f;
        if ((buttons & MoveButtons.Down) != 0)
            vec.Y -= 1f;
        if ((buttons & MoveButtons.Left) != 0)
            vec.X -= 1f;
        if ((buttons & MoveButtons.Right) != 0)
            vec.X += 1f;
        return vec;
    }
}
