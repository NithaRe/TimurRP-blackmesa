using System.Numerics;
using Content.Shared._BlackM.WalkBob;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Standing;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.WalkBob;

public sealed class WalkBobSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;

    private const float RunBlendSpeed = 6f;
    private const float KickCooldown = 0.04f;
    private const float FallbackSprintThreshold = 3.5f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WalkBobComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<GunComponent, GunShotEvent>(OnGunShot);

        SubscribeNetworkEvent<MuzzleFlashEvent>(OnMuzzleFlash);
    }

    private void OnShutdown(EntityUid uid, WalkBobComponent component, ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(uid, out var sprite))
            Apply(uid, sprite, component, Vector2.One, Vector2.Zero, Angle.Zero);
    }

    #region fire

    private void OnGunShot(EntityUid gunUid, GunComponent gun, ref GunShotEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        var user = args.User;
        if (!HasComp<WalkBobComponent>(user))
            return;

        Vector2 dir;
        if (gun.ShootCoordinates is { } coords && coords.IsValid(EntityManager))
        {
            var from = _transform.GetMapCoordinates(user).Position;
            var to = _transform.ToMapCoordinates(coords).Position;
            dir = to - from;
        }
        else
        {
            dir = FacingVector(user);
        }

        if (dir.LengthSquared() < 0.0001f)
            dir = FacingVector(user);

        Kick(user, Vector2.Normalize(dir));
    }

    private void OnMuzzleFlash(MuzzleFlashEvent ev)
    {
        var gun = GetEntity(ev.Uid);
        if (!Exists(gun))
            return;

        var user = Transform(gun).ParentUid;
        if (!HasComp<WalkBobComponent>(user))
            return;

        Kick(user, FacingVector(user));
    }

    private void Kick(EntityUid user, Vector2 worldDir)
    {
        if (!TryComp<WalkBobComponent>(user, out var bob))
            return;

        if (_timing.CurTime < bob.NextKickTime)
            return;
        bob.NextKickTime = _timing.CurTime + TimeSpan.FromSeconds(KickCooldown);

        var screenDir = WorldToScreen(worldDir);
        var vel = bob.RecoilVelocity - screenDir * bob.RecoilImpulse;

        var max = bob.RecoilImpulse * 1.5f;
        if (vel.LengthSquared() > max * max)
            vel = Vector2.Normalize(vel) * max;

        bob.RecoilVelocity = vel;
    }

    #endregion

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var dt = MathF.Min(frameTime, 0.1f);

        var query = EntityQueryEnumerator<WalkBobComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var bob, out var sprite))
        {
            UpdateBob(uid, bob, sprite, dt);
        }
    }

    private void UpdateBob(EntityUid uid, WalkBobComponent bob, SpriteComponent sprite, float dt)
    {
        var canAnimate = CanAnimate(uid);

        float speed;

        if (bob.VisualSpeedOverride is { } over)
        {
            speed = over;
        }
        else if (TryComp<PhysicsComponent>(uid, out var physics))
        {
            var vel = physics.LinearVelocity;
            speed = vel.Length();
        }
        else
        {
            speed = 0f;
        }

        var moving = canAnimate && speed > bob.MinSpeedThreshold;
        var running = moving && speed >= GetSprintThreshold(uid, bob);

        var kBlend = 1f - MathF.Exp(-bob.ReturnLerpSpeed * dt);
        var kRun = 1f - MathF.Exp(-RunBlendSpeed * dt);

        bob.ActiveWeight = Lerp(bob.ActiveWeight, canAnimate ? 1f : 0f, kBlend);
        bob.MoveWeight = Lerp(bob.MoveWeight, moving ? 1f : 0f, kBlend);
        bob.RunWeight = Lerp(bob.RunWeight, running ? 1f : 0f, kRun);

        var pulling = canAnimate
                      && TryComp<PullerComponent>(uid, out var puller)
                      && puller.Pulling is { } pulled
                      && Exists(pulled);

        bob.PullWeight = Lerp(bob.PullWeight, pulling ? 1f : 0f, kBlend);

        var stride = Lerp(1f, bob.RunStrideMult, bob.RunWeight)
                     * Lerp(1f, bob.PullStrideMult, bob.PullWeight);

        if (moving)
            bob.Phase = (bob.Phase + speed * dt * bob.PhasePerMeter * stride) % MathF.Tau;
        else if (bob.MoveWeight < 0.01f)
            bob.Phase = 0f;

        bob.IdleTime += dt;

        if (bob.RecoilOffset != Vector2.Zero || bob.RecoilVelocity != Vector2.Zero)
        {
            var sdt = MathF.Min(dt, 1f / 30f);
            var acc = -bob.RecoilOffset * bob.RecoilStiffness - bob.RecoilVelocity * bob.RecoilDamping;
            bob.RecoilVelocity += acc * sdt;
            bob.RecoilOffset += bob.RecoilVelocity * sdt;

            if (bob.RecoilOffset.LengthSquared() < 1e-8f && bob.RecoilVelocity.LengthSquared() < 1e-6f)
            {
                bob.RecoilOffset = Vector2.Zero;
                bob.RecoilVelocity = Vector2.Zero;
            }
        }

        var w = bob.MoveWeight;
        var r = bob.RunWeight;
        var p = bob.PullWeight;
        var a = bob.ActiveWeight;

        var s = MathF.Sin(bob.Phase);
        var hop = MathF.Abs(s);

        var bobHeight = Lerp(bob.WalkBobHeight, bob.RunBobHeight, r) * Lerp(1f, 0.7f, p);
        var swayDeg = Lerp(bob.WalkSway, bob.RunSway, r);
        var squash = Lerp(bob.WalkSquash, bob.RunSquash, r);

        var centeredHop = hop - 0.637f;
        var offset = new Vector2(0f, centeredHop * bobHeight) * w;
        offset.X += MathF.Sin(bob.Phase - 0.7f) * 0.025f * p * w;
        var runAccent = Lerp(0.35f, 1f, r);
        var rotDeg = s * swayDeg * w * runAccent;
        var stretch = (hop - 0.5f) * squash * w * runAccent;

        var breath = MathF.Sin(bob.IdleTime * bob.BreathSpeed + uid.Id * 0.7f);
        stretch += breath * bob.BreathAmplitude * (1f - w) * a;
        offset.Y += breath * bob.BreathAmplitude * 0.18f * (1f - w) * a;

        if (bob.RecoilOffset != Vector2.Zero)
        {
            offset += bob.RecoilOffset;
            rotDeg -= bob.RecoilOffset.X * bob.RecoilTilt;
            stretch -= bob.RecoilOffset.Length() * bob.RecoilSquash;
        }

        var scale = new Vector2(1f - stretch * 0.5f, 1f + stretch);
        Apply(uid, sprite, bob, scale, offset, Angle.FromDegrees(rotDeg));
    }

    private bool CanAnimate(EntityUid uid)
    {
        if (TryComp<MobStateComponent>(uid, out var mobState) && mobState.CurrentState != MobState.Alive)
            return false;

        return !_standing.IsDown(uid);
    }

    private float GetSprintThreshold(EntityUid uid, WalkBobComponent bob)
    {
        if (bob.SprintSpeedThreshold is { } threshold)
            return threshold;

        if (TryComp<MovementSpeedModifierComponent>(uid, out var mod)
            && mod.CurrentSprintSpeed > mod.CurrentWalkSpeed * 1.1f)
        {
            return mod.CurrentWalkSpeed + (mod.CurrentSprintSpeed - mod.CurrentWalkSpeed) * 0.6f;
        }

        return FallbackSprintThreshold;
    }

    private void Apply(EntityUid uid, SpriteComponent sprite, WalkBobComponent bob,
        Vector2 scale, Vector2 offset, Angle rotation)
    {
        int? layer = null;
        if (bob.TargetLayerKey != null && sprite.LayerMapTryGet(bob.TargetLayerKey, out var index))
            layer = index;

        var onLayer = layer != null;
        if (onLayer != bob.AppliedOnLayer)
        {
            bob.AppliedScale = Vector2.One;
            bob.AppliedOffset = Vector2.Zero;
            bob.AppliedRotation = Angle.Zero;
            bob.AppliedOnLayer = onLayer;
        }

        if (ApproxEq(scale, bob.AppliedScale)
            && ApproxEq(offset, bob.AppliedOffset)
            && MathF.Abs((float) (rotation - bob.AppliedRotation).Theta) < 0.0001f)
        {
            return;
        }

        Vector2 curScale;
        Vector2 curOffset;
        Angle curRotation;

        if (layer is { } l)
        {
            var layerData = (SpriteComponent.Layer) sprite[l];
            curScale = layerData.Scale;
            curOffset = layerData.Offset;
            curRotation = layerData.Rotation;
        }
        else
        {
            curScale = sprite.Scale;
            curOffset = sprite.Offset;
            curRotation = sprite.Rotation;
        }

        var baseScale = new Vector2(
            SafeDiv(curScale.X, bob.AppliedScale.X),
            SafeDiv(curScale.Y, bob.AppliedScale.Y));
        var baseOffset = curOffset - bob.AppliedOffset;
        var baseRotation = curRotation - bob.AppliedRotation;

        var newScale = baseScale * scale;
        var newOffset = baseOffset + offset;
        var newRotation = baseRotation + rotation;

        if (layer is { } li)
        {
            _sprite.LayerSetScale((uid, sprite), li, newScale);
            _sprite.LayerSetOffset((uid, sprite), li, newOffset);
            _sprite.LayerSetRotation((uid, sprite), li, newRotation);
        }
        else
        {
            _sprite.SetScale((uid, sprite), newScale);
            _sprite.SetOffset((uid, sprite), newOffset);
            _sprite.SetRotation((uid, sprite), newRotation);
        }

        bob.AppliedScale = scale;
        bob.AppliedOffset = offset;
        bob.AppliedRotation = rotation;
    }

    #region Helpers

    private Vector2 WorldToScreen(Vector2 worldVec)
    {
        var rot = _eye.CurrentEye.Rotation;
        return (-rot).RotateVec(worldVec);
    }

    private Vector2 FacingVector(EntityUid uid)
    {
        return _transform.GetWorldRotation(uid).GetDir().ToVec();
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static float SafeDiv(float value, float divisor)
        => MathF.Abs(divisor) > 1e-4f ? value / divisor : value;

    private static bool ApproxEq(Vector2 a, Vector2 b)
        => Vector2.DistanceSquared(a, b) < 1e-8f;

    #endregion
}
