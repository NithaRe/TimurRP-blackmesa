using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.SpawnMarkers;

public sealed class SpawnMarkerSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing   = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MobSpawnMarkerComponent, ComponentStartup>(OnMobMarkerStartup);
        SubscribeLocalEvent<MobSpawnRadiusMarkerComponent, ComponentStartup>(OnMobRadiusMarkerStartup);
        SubscribeLocalEvent<ItemSpawnMarkerComponent, ComponentStartup>(OnItemMarkerStartup);
        SubscribeLocalEvent<ItemSpawnRadiusMarkerComponent, ComponentStartup>(OnItemRadiusMarkerStartup);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;

        var q1 = EntityQueryEnumerator<MobSpawnMarkerComponent>();
        while (q1.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextSpawnTime)
                continue;

            comp.NextSpawnTime = now + TimeSpan.FromSeconds(comp.RespawnDelay);
            SpawnMobOnMarker(uid, comp);
        }

        var q2 = EntityQueryEnumerator<MobSpawnRadiusMarkerComponent>();
        while (q2.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextSpawnTime)
                continue;

            comp.NextSpawnTime = now + TimeSpan.FromSeconds(comp.RespawnDelay);
            SpawnMobRadius(uid, comp);
        }
    }

    private void OnMobMarkerStartup(EntityUid uid, MobSpawnMarkerComponent comp, ComponentStartup args)
    {
        for (var i = 0; i < comp.Count; i++)
            SpawnMobOnMarker(uid, comp);

        comp.NextSpawnTime = _timing.CurTime + TimeSpan.FromSeconds(comp.RespawnDelay);
    }

    private void OnMobRadiusMarkerStartup(EntityUid uid, MobSpawnRadiusMarkerComponent comp, ComponentStartup args)
    {
        for (var i = 0; i < comp.Count; i++)
            SpawnMobRadius(uid, comp);

        comp.NextSpawnTime = _timing.CurTime + TimeSpan.FromSeconds(comp.RespawnDelay);
    }

    private void OnItemMarkerStartup(EntityUid uid, ItemSpawnMarkerComponent comp, ComponentStartup args)
    {
        if (comp.Prototypes.Count == 0)
            return;
        var xform = Transform(uid);
        for (var i = 0; i < comp.Count; i++)
            Spawn(_random.Pick(comp.Prototypes), xform.Coordinates);
    }

    private void OnItemRadiusMarkerStartup(EntityUid uid, ItemSpawnRadiusMarkerComponent comp, ComponentStartup args)
    {
        if (comp.Prototypes.Count == 0)
            return;
        var xform = Transform(uid);
        for (var i = 0; i < comp.Count; i++)
        {
            var offset = _random.NextVector2(-comp.Radius, comp.Radius);
            Spawn(_random.Pick(comp.Prototypes), xform.Coordinates.Offset(offset));
        }
    }

    private void SpawnMobOnMarker(EntityUid markerUid, MobSpawnMarkerComponent comp)
    {
        var xform = Transform(markerUid);
        for (var i = 0; i < comp.Count; i++)
            Spawn(comp.Prototype, xform.Coordinates);
    }

    private void SpawnMobRadius(EntityUid markerUid, MobSpawnRadiusMarkerComponent comp)
    {
        var xform = Transform(markerUid);
        for (var i = 0; i < comp.Count; i++)
        {
            var offset = _random.NextVector2(-comp.Radius, comp.Radius);
            Spawn(comp.Prototype, xform.Coordinates.Offset(offset));
        }
    }
}
