using System.Numerics;
using Content.Server.Station.Systems;
using Content.Shared._BlackM.ZenCatalyst;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.ZenCatalyst;
public sealed class ZenCatalystSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZenCatalystComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, ZenCatalystComponent component, ComponentStartup args)
    {
        component.NextSpawnTime = _timing.CurTime + component.SpawnInterval;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<ZenCatalystComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var catalyst, out var xform))
        {
            if (!catalyst.Enabled)
                continue;

            if (curTime < catalyst.NextSpawnTime)
                continue;

            catalyst.NextSpawnTime = curTime + catalyst.SpawnInterval;

            SpawnZenWave(uid, catalyst, xform);
        }
    }

    private void SpawnZenWave(EntityUid uid, ZenCatalystComponent catalyst, TransformComponent xform)
    {
        if (catalyst.ZenMobPrototypes.Count == 0)
            return;

        var gridUid = xform.GridUid;
        if (gridUid == null || !TryComp<MapGridComponent>(gridUid.Value, out var grid))
            return;

        if (!IsStationGrid(gridUid.Value))
            return;

        for (var i = 0; i < catalyst.SpawnAmount; i++)
        {
            if (!TryGetRandomGridCoordinates(gridUid.Value, grid, catalyst, out var coords))
                continue;

            var protoId = _random.Pick(catalyst.ZenMobPrototypes);
            Spawn(protoId, coords);
        }
    }

    private bool TryGetRandomGridCoordinates(EntityUid gridUid, MapGridComponent grid, ZenCatalystComponent catalyst, out EntityCoordinates coords)
    {
        coords = default;

        var tiles = new List<Vector2i>();
        foreach (var tileRef in _map.GetAllTiles(gridUid, grid))
        {
            if (tileRef.Tile.IsEmpty)
                continue;

            tiles.Add(tileRef.GridIndices);
        }

        if (tiles.Count == 0)
            return false;

        for (var attempt = 0; attempt < catalyst.MaxPlacementAttempts; attempt++)
        {
            var tileIndices = _random.Pick(tiles);

            var localX = tileIndices.X + _random.NextFloat(0.15f, 0.85f);
            var localY = tileIndices.Y + _random.NextFloat(0.15f, 0.85f);

            var candidate = new EntityCoordinates(gridUid, new Vector2(localX, localY));

            if (_lookup.AnyEntitiesIntersecting(candidate, LookupFlags.Static))
                continue;

            coords = candidate;
            return true;
        }

        return false;
    }

    private bool IsStationGrid(EntityUid gridUid)
    {
        return _station.GetOwningStation(gridUid) != null;
    }
}
