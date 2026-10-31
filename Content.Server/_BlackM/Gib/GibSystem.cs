using System.Numerics;
using Content.Server.Body.Systems;
using Content.Shared._BlackM.Gib;
using Content.Shared.Camera;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Map;
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
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();
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

        RaiseNetworkEvent(
            new GibBurstEvent(mapPos.MapId, mapPos.Position, comp.BurstStrength, comp.BloodColor),
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

        SpawnSplats(comp, coords);

        var gibs = _body.GibBody(ent.Owner, gibOrgans: false, launchGibs: comp.LaunchParts,
            splatModifier: comp.PartsScatter);

        foreach (var gib in gibs)
            SetupFade(gib, comp.PartsLifetime, comp.FadeDuration, 0.4f);

        if (!TerminatingOrDeleted(ent.Owner))
            QueueDel(ent.Owner);
    }

    private void Shockwave(EntityUid source, MapCoordinates center, float range, float force)
    {
        foreach (var body in _lookup.GetEntitiesInRange<PhysicsComponent>(center, range))
        {
            if (body.Owner == source || body.Comp.BodyType != BodyType.Dynamic)
                continue;

            var offset = _xform.GetWorldPosition(body.Owner) - center.Position;
            var dist = offset.Length();
            if (dist >= range)
                continue;

            var dir = dist > 0.01f ? offset / dist : _random.NextAngle().ToVec();
            var falloff = 1f - dist / range;

            var impulse = dir * force * falloff * body.Comp.Mass;
            _physics.ApplyLinearImpulse(body.Owner, impulse, body: body.Comp);
        }
    }

    private void SpawnSplats(GibComponent comp, EntityCoordinates coords)
    {
        if (comp.Splats.Count == 0)
            return;

        for (var i = 0; i < comp.SplatCount; i++)
        {
            var dist = comp.SplatRadius * MathF.Sqrt(_random.NextFloat());
            var offset = _random.NextAngle().ToVec() * dist;
            var proto = _random.Pick(comp.Splats);
            var delay = TimeSpan.FromSeconds(0.05 + dist / comp.SplatRadius * 0.35 + _random.NextFloat() * 0.1);
            var lifetime = comp.SplatLifetime;
            var spawnAt = coords.Offset(offset);

            Timer.Spawn(delay, () =>
            {
                var splat = Spawn(proto, spawnAt);
                _xform.SetLocalRotation(splat, _random.NextAngle());
                SetupFade(splat, lifetime, 4f, 1f);
            });
        }
    }

    private void SetupFade(EntityUid uid, float lifetime, float fadeDuration, float endScale)
    {
        var despawn = EnsureComp<TimedDespawnComponent>(uid);
        despawn.Lifetime = lifetime;

        var fade = EnsureComp<GibFadeComponent>(uid);
        fade.StartTime = _timing.CurTime;
        fade.Lifetime = lifetime;
        fade.FadeDuration = Math.Min(fadeDuration, lifetime);
        fade.EndScale = endScale;
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
