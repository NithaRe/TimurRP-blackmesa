using System.Numerics;
using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Shared._BlackM.ZenCatalyst;
using Content.Shared.GameTicking;
using Robust.Server.GameStates;
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
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly PvsOverrideSystem _pvsOverride = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    private EntityUid? _statusEntity;
    private float _statusAccumulator;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZenCatalystComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnStartup(EntityUid uid, ZenCatalystComponent component, ComponentStartup args)
    {
        component.NextSpawnTime = _timing.CurTime + component.SpawnInterval;
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _statusEntity = null;
        _statusAccumulator = 0;
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

        UpdateStatus(frameTime);
    }

    private void UpdateStatus(float frameTime)
    {
        if (_gameTicker.RunLevel != GameRunLevel.InRound)
            return;

        _statusAccumulator -= frameTime;
        if (_statusAccumulator > 0)
            return;
        _statusAccumulator = 1f;

        if (_statusEntity is not { } statusUid || !Exists(statusUid))
        {
            // Broadcast only the HUD summary, not the physical catalyst and its surroundings.
            statusUid = Spawn(null, MapCoordinates.Nullspace);
            _statusEntity = statusUid;
            AddComp<ZenCatalystStatusComponent>(statusUid);
            _pvsOverride.AddGlobalOverride(statusUid);
        }

        var status = Comp<ZenCatalystStatusComponent>(statusUid);
        var catalysts = new List<ZenCatalystWaveStatus>();
        var query = EntityQueryEnumerator<ZenCatalystComponent, TransformComponent>();
        while (query.MoveNext(out _, out var catalyst, out var xform))
        {
            if (!catalyst.Enabled || catalyst.SpawnAmount <= 0 || catalyst.ZenMobPrototypes.Count == 0 ||
                xform.GridUid is not { } gridUid || !HasComp<MapGridComponent>(gridUid) || !IsStationGrid(gridUid))
                continue;

            catalysts.Add(new ZenCatalystWaveStatus(catalyst.SpawnInterval, catalyst.SpawnAmount, catalyst.NextSpawnTime));
        }

        catalysts.Sort((a, b) =>
        {
            var result = a.NextSpawnTime.CompareTo(b.NextSpawnTime);
            if (result != 0)
                return result;
            result = a.SpawnInterval.CompareTo(b.SpawnInterval);
            return result != 0 ? result : a.SpawnAmount.CompareTo(b.SpawnAmount);
        });
        if (status.Catalysts.SequenceEqual(catalysts))
            return;
        status.Catalysts = catalysts;
        Dirty(statusUid, status);
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

        var spawned = false;
        for (var i = 0; i < catalyst.SpawnAmount; i++)
        {
            if (!TryGetRandomGridCoordinates(gridUid.Value, grid, catalyst, out var coords))
                continue;

            var protoId = _random.Pick(catalyst.ZenMobPrototypes);
            Spawn(protoId, coords);
            spawned = true;
        }

        if (spawned)
            _chat.DispatchStationAnnouncement(
                uid,
                Loc.GetString("zen-catalyst-announce-wave"),
                Loc.GetString("zen-catalyst-announcer"),
                playDefaultSound: false);
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
