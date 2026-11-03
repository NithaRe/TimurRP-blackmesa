using System.Numerics;
using Content.Server.Body.Systems;
using Content.Shared._BlackM.Gib;
using Content.Shared.Camera;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Physics;
using Robust.Server.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.Gib;

public sealed class GibSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _recoil = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapMan = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IComponentFactory _compFactory = default!;

    private EntityQuery<OccluderComponent> _occluders;

    private const float ClearanceRadius = 1.8f;

    public override void Initialize()
    {
        base.Initialize();
        _occluders = GetEntityQuery<OccluderComponent>();
        SubscribeLocalEvent<GibComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(Entity<GibComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        var comp = ent.Comp;
        var coords = _xform.GetMoverCoordinates(ent.Owner);
        var mapPos = _xform.ToMapCoordinates(coords);

        if (comp.DeleteItems)
        {
            var items = new List<EntityUid>();
            foreach (var item in _inventory.GetHandOrInventoryEntities(ent.Owner))
                items.Add(item);
            foreach (var item in items)
                QueueDel(item);
        }

        var (reach, push) = ComputeClearance(mapPos);
        var burstPos = mapPos.Position + push;

        RaiseNetworkEvent(
            new GibBurstEvent(mapPos.MapId, burstPos, comp.BurstStrength, comp.BloodColor, reach,
                comp.Chunks, comp.Nova, comp.ScreenEffect),
            Filter.Pvs(coords, entityMan: EntityManager));

        if (comp.Sound != null)
            _audio.PlayPvs(comp.Sound, coords);

        if (comp.DropsSound != null)
        {
            var dropsSound = comp.DropsSound;
            Timer.Spawn(TimeSpan.FromSeconds(comp.DropsDelay), () => _audio.PlayPvs(dropsSound, coords));
        }

        if (comp.ShakeStrength > 0)
        {
            ShakeNearby(coords, comp.ShakeRadius, comp.ShakeStrength);

            var shakeR = comp.ShakeRadius;
            var shakeS = comp.ShakeStrength;
            Timer.Spawn(TimeSpan.FromSeconds(0.08), () => ShakeNearby(coords, shakeR, shakeS * 0.55f));
            Timer.Spawn(TimeSpan.FromSeconds(0.17), () => ShakeNearby(coords, shakeR, shakeS * 0.35f));
            Timer.Spawn(TimeSpan.FromSeconds(0.30), () => ShakeNearby(coords, shakeR, shakeS * 0.18f));
        }

        if (comp.ShockRange > 0 && comp.ShockForce > 0)
            Shockwave(ent.Owner, mapPos, comp.ShockRange, comp.ShockForce);

        SpawnSplats(comp, mapPos);

        if (comp.SuppressBlood && _compFactory.TryGetRegistration("Bloodstream", out var bloodstream))
            RemComp(ent.Owner, bloodstream.Type);

        var gibs = _body.GibBody(ent.Owner, gibOrgans: false, launchGibs: false);

        foreach (var gib in gibs)
        {
            SetupFade(gib, comp.PartsLifetime, comp.FadeDuration, 0.4f, 0f);

            if (comp.LaunchParts)
                LaunchPart(gib, comp.PartsSpeed, comp.PartsSpin);

            if (comp.BloodTrail)
            {
                var trail = EnsureComp<GibTrailComponent>(gib);
                trail.Color = comp.BloodColor;
                Dirty(gib, trail);
            }
        }

        if (!TerminatingOrDeleted(ent.Owner))
            QueueDel(ent.Owner);
    }

    private void LaunchPart(EntityUid part, float speed, float spin)
    {
        if (!TryComp<PhysicsComponent>(part, out var body))
            return;

        var dir = _random.NextAngle().ToVec();
        var v = speed * _random.NextFloat(0.55f, 1.1f);

        _physics.SetLinearVelocity(part, dir * v, body: body);
        _physics.SetAngularVelocity(part, _random.NextFloat(-spin, spin), body: body);
    }

    private bool RayBlocked(MapId map, Vector2 from, Vector2 to, out float distance)
    {
        var delta = to - from;
        var len = delta.Length();
        distance = len;

        if (len < 0.05f)
            return false;

        var ray = new CollisionRay(from, delta / len, (int) CollisionGroup.Opaque);
        var hitAny = false;

        foreach (var hit in _physics.IntersectRay(map, ray, len, returnOnFirstHit: false))
        {
            if (!_occluders.TryComp(hit.HitEntity, out var occluder) || !occluder.Enabled)
                continue;

            if (hit.Distance < distance)
            {
                distance = hit.Distance;
                hitAny = true;
            }
        }

        return hitAny;
    }

    private (float Reach, Vector2 Push) ComputeClearance(MapCoordinates center)
    {
        var minDist = ClearanceRadius;
        var push = Vector2.Zero;

        for (var i = 0; i < 8; i++)
        {
            var angle = i * MathF.PI / 4f;
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));

            if (!RayBlocked(center.MapId, center.Position, center.Position + dir * ClearanceRadius, out var dist))
                continue;

            minDist = MathF.Min(minDist, dist);
            push -= dir * (ClearanceRadius - dist) * 0.30f;
        }

        if (push.LengthSquared() > 0.36f)
            push = Vector2.Normalize(push) * 0.6f;

        return (Math.Clamp(minDist / ClearanceRadius, 0.55f, 1f), push);
    }

    private void Shockwave(EntityUid source, MapCoordinates center, float range, float force)
    {
        foreach (var body in _lookup.GetEntitiesInRange<PhysicsComponent>(center, range))
        {
            if (body.Owner == source || body.Comp.BodyType != BodyType.Dynamic)
                continue;

            var targetPos = _xform.GetWorldPosition(body.Owner);
            var offset = targetPos - center.Position;
            var dist = offset.Length();
            if (dist >= range)
                continue;

            if (RayBlocked(center.MapId, center.Position, targetPos, out _))
                continue;

            var dir = dist > 0.01f ? offset / dist : _random.NextAngle().ToVec();
            var falloff = 1f - dist / range;

            var impulse = dir * force * falloff * body.Comp.Mass;
            _physics.ApplyLinearImpulse(body.Owner, impulse, body: body.Comp);
        }
    }

    private void SpawnSplats(GibComponent comp, MapCoordinates center)
    {
        if (comp.Splats.Count == 0)
            return;

        for (var i = 0; i < comp.SplatCount; i++)
        {
            var dist = comp.SplatRadius * MathF.Sqrt(_random.NextFloat());
            var dir = _random.NextAngle().ToVec();
            var target = center.Position + dir * dist;

            if (RayBlocked(center.MapId, center.Position, target, out var hit))
            {
                if (hit < 0.5f)
                    continue;

                dist = hit - 0.35f;
                target = center.Position + dir * dist;
            }

            var at = new MapCoordinates(target, center.MapId);
            if (!HasFloor(at))
                continue;

            var proto = _random.Pick(comp.Splats);
            var delay = TimeSpan.FromSeconds(0.05 + dist / MathF.Max(comp.SplatRadius, 0.1f) * 0.35 + _random.NextFloat() * 0.1);
            var lifetime = comp.SplatLifetime;

            Timer.Spawn(delay, () =>
            {
                var splat = Spawn(proto, at);
                _xform.SetLocalRotation(splat, _random.NextAngle());
                SetupFade(splat, lifetime, 4f, 1f, 0.2f);
            });
        }
    }

    private bool HasFloor(MapCoordinates coords)
    {
        if (!_mapMan.TryFindGridAt(coords, out var gridUid, out var grid))
            return false;

        var tile = _map.GetTileRef(gridUid, grid, _map.MapToGrid(gridUid, coords));
        return !tile.Tile.IsEmpty;
    }

    private void SetupFade(EntityUid uid, float lifetime, float fadeDuration, float endScale, float growTime)
    {
        var despawn = EnsureComp<TimedDespawnComponent>(uid);
        despawn.Lifetime = lifetime;

        var fade = EnsureComp<GibFadeComponent>(uid);
        fade.StartTime = _timing.CurTime;
        fade.Lifetime = lifetime;
        fade.FadeDuration = Math.Min(fadeDuration, lifetime);
        fade.EndScale = endScale;
        fade.GrowTime = growTime;
        Dirty(uid, fade);
    }

    private void ShakeNearby(EntityCoordinates coords, float radius, float strength)
    {
        var mapPos = _xform.ToMapCoordinates(coords);

        foreach (var actor in _lookup.GetEntitiesInRange<ActorComponent>(mapPos, radius))
        {
            var dist = (_xform.GetWorldPosition(actor.Owner) - mapPos.Position).Length();
            var falloff = 1f - Math.Clamp(dist / radius, 0f, 1f);
            if (falloff <= 0f)
                continue;

            var dir = _random.NextAngle().ToVec() * strength * falloff;
            _recoil.KickCamera(actor.Owner, dir);
        }
    }
}
