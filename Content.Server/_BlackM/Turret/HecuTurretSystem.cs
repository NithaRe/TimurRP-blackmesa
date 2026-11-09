using System.Numerics;
using Content.Shared._BlackM.Turret;
using Content.Shared.Audio;
using Content.Shared.Examine;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.Turret;

public sealed class HecuTurretSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambient = default!;

    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HecuTurretComponent, GunShotEvent>(OnGunShot);
    }

    private void OnGunShot(Entity<HecuTurretComponent> ent, ref GunShotEvent args)
    {
        ent.Comp.ShotCount++;
        Dirty(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<HecuTurretComponent, GunComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var turret, out var gun, out var xform))
        {
            UpdateRotation(turret, xform, frameTime);

            if (now < turret.NextUpdate)
                continue;

            turret.NextUpdate = now + turret.UpdateInterval;

            // Disabled when unanchored
            if (turret.RequireAnchored && !xform.Anchored)
            {
                SetTarget(uid, turret, null);
                SetState(uid, turret, HecuTurretState.Idle);
                SetAmbience(uid, turret, false);
                continue;
            }

            SetAmbience(uid, turret, true);

            var target = turret.Target;
            if (target != null && !IsValidTarget(uid, turret, target.Value))
                target = null;

            if (target == null && now >= turret.NextScan)
            {
                turret.NextScan = now + turret.ScanInterval;
                target = FindTarget(uid, turret, xform);
            }

            SetTarget(uid, turret, target);

            if (target == null)
            {
                UpdateNoTarget(uid, turret, now);
                continue;
            }

            turret.LastSeen = now;

            switch (turret.State)
            {
                case HecuTurretState.Idle:
                    turret.PrepareEnd = now + turret.PrepareTime;
                    SetState(uid, turret, HecuTurretState.Preparing);
                    _audio.PlayPvs(turret.PrepareSound, uid);
                    break;

                case HecuTurretState.Preparing:
                    if (now < turret.PrepareEnd)
                        break;

                    SetState(uid, turret, HecuTurretState.Armed);
                    TryShoot(uid, turret, gun, xform, target.Value);
                    break;

                case HecuTurretState.Armed:
                    TryShoot(uid, turret, gun, xform, target.Value);
                    break;
            }
        }
    }

    private void UpdateRotation(HecuTurretComponent turret, TransformComponent xform, float frameTime)
    {
        if (turret.State != HecuTurretState.Idle &&
            turret.Target is { } target &&
            TryGetGoal(xform, target, out var goal))
        {
            var step = Angle.FromDegrees(turret.RotationSpeed).Theta * frameTime;
            turret.Rotation = HecuTurretMath.RotateTowards(turret.Rotation, goal, step);
            return;
        }

        if (turret.State == HecuTurretState.Idle && turret.IdleRotateSpeed > 0f)
        {
            var step = Angle.FromDegrees(turret.IdleRotateSpeed).Theta * frameTime;
            turret.Rotation = HecuTurretMath.Spin(turret.Rotation, step);
        }
    }

    private bool TryGetGoal(TransformComponent xform, EntityUid target, out Angle goal)
    {
        goal = Angle.Zero;
        if (TerminatingOrDeleted(target))
            return false;

        var delta = _transform.GetWorldPosition(target) - _transform.GetWorldPosition(xform);
        if (delta.LengthSquared() < 0.0001f)
            return false;

        goal = delta.ToWorldAngle() - _transform.GetWorldRotation(xform);
        return true;
    }

    private void UpdateNoTarget(EntityUid uid, HecuTurretComponent turret, TimeSpan now)
    {
        if (turret.State == HecuTurretState.Idle)
            return;

        if (now - turret.LastSeen >= turret.DisarmDelay)
        {
            SetState(uid, turret, HecuTurretState.Idle);
            _audio.PlayPvs(turret.FoldSound, uid);
            return;
        }

        // Finish the windup and stay deployed for a while
        if (turret.State == HecuTurretState.Preparing && now >= turret.PrepareEnd)
            SetState(uid, turret, HecuTurretState.Armed);
    }

    private void TryShoot(EntityUid uid, HecuTurretComponent turret, GunComponent gun, TransformComponent xform, EntityUid target)
    {
        // Fire only when the barrel is on target
        if (!TryGetGoal(xform, target, out var goal) ||
            HecuTurretMath.Distance(turret.Rotation, goal) > Angle.FromDegrees(turret.FireTolerance).Theta)
        {
            return;
        }

        if (!_gun.CanShoot(gun))
            return;

        var coords = new EntityCoordinates(target, Vector2.Zero);
        _gun.AttemptShoot(uid, uid, gun, coords, target);
    }

    private EntityUid? FindTarget(EntityUid uid, HecuTurretComponent turret, TransformComponent xform)
    {
        _mobs.Clear();
        _lookup.GetEntitiesInRange(xform.Coordinates, turret.Range, _mobs);

        var origin = _transform.GetWorldPosition(xform);
        EntityUid? best = null;
        var bestDist = float.MaxValue;

        foreach (var mob in _mobs)
        {
            var dist = (_transform.GetWorldPosition(mob.Owner) - origin).LengthSquared();
            if (dist >= bestDist)
                continue;

            if (!IsValidTarget(uid, turret, mob.Owner))
                continue;

            best = mob.Owner;
            bestDist = dist;
        }

        _mobs.Clear();
        return best;
    }

    private bool IsValidTarget(EntityUid uid, HecuTurretComponent turret, EntityUid target)
    {
        if (target == uid || TerminatingOrDeleted(target))
            return false;

        if (!TryComp<MobStateComponent>(target, out var mobState))
            return false;

        if (_mobState.IsDead(target, mobState))
            return false;

        if (!turret.AttackCritical && _mobState.IsCritical(target, mobState))
            return false;

        foreach (var tag in turret.IgnoreTags)
        {
            if (_tag.HasTag(target, tag))
                return false;
        }

        if (_container.IsEntityOrParentInContainer(target))
            return false;

        return _examine.InRangeUnOccluded(uid, target, turret.Range);
    }

    private void SetTarget(EntityUid uid, HecuTurretComponent turret, EntityUid? target)
    {
        if (turret.Target == target)
            return;

        turret.Target = target;
        Dirty(uid, turret);
    }

    private void SetState(EntityUid uid, HecuTurretComponent turret, HecuTurretState state)
    {
        if (turret.State == state)
            return;

        turret.State = state;
        Dirty(uid, turret);

        var loop = state == HecuTurretState.Idle ? turret.IdleLoopSound : turret.ActiveLoopSound;
        if (loop != null)
            _ambient.SetSound(uid, loop);
    }

    private void SetAmbience(EntityUid uid, HecuTurretComponent turret, bool enabled)
    {
        if (turret.AmbienceOn == enabled)
            return;

        turret.AmbienceOn = enabled;
        _ambient.SetAmbience(uid, enabled);
    }
}
