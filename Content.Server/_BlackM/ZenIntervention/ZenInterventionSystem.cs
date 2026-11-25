using System.Linq;
using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._BlackM.ZenIntervention;
using Content.Shared.GameTicking;
using Content.Shared.Station.Components;
using Robust.Server.GameStates;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.ZenIntervention;

public sealed partial class ZenInterventionSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private PvsOverrideSystem _pvsOverride = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private GameTicker _gameTicker = default!;

    private const float SyncInterval = 1f;
    private static readonly TimeSpan AmsSurgeDuration = TimeSpan.FromMinutes(1);

    private EntityUid? _singleton;
    private bool _roundWasActive;
    private float _syncAccumulator;
    private readonly Queue<EntProtoId> _amsSpawns = new();
    private TimeSpan _amsSpawnInterval;
    private TimeSpan _nextAmsSpawn;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _singleton = null;
        _roundWasActive = false;
        _syncAccumulator = 0f;
        _amsSpawns.Clear();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_gameTicker.RunLevel != GameRunLevel.InRound)
        {
            _roundWasActive = false;
            return;
        }

        if (!_roundWasActive)
        {
            _roundWasActive = true;
            ResetState();
        }

        EnsureSingleton();

        if (_singleton is not { } uid || !TryComp<ZenInterventionComponent>(uid, out var comp))
            return;

        if (!comp.Enabled)
            return;

        Grow(uid, comp, frameTime);
        CheckBreach(comp);
        CheckWave(comp);
        UpdateAmsSurge(comp);
    }

    private void Grow(EntityUid uid, ZenInterventionComponent comp, float frameTime)
    {
        if (comp.Level >= comp.MaxLevel)
            return;

        var gainPerSecond = comp.GainPerMinute / 60f;
        var newLevel = comp.Level + gainPerSecond * frameTime;

        comp.Level = newLevel >= comp.MaxLevel ? comp.MaxLevel : newLevel;

        _syncAccumulator += frameTime;
        if (_syncAccumulator >= SyncInterval || comp.Level >= comp.MaxLevel)
        {
            _syncAccumulator = 0f;
            Dirty(uid, comp);
        }
    }

    private void CheckBreach(ZenInterventionComponent comp, bool gradual = false)
    {
        foreach (var threshold in comp.IntermediateBreachThresholds)
        {
            if (threshold <= 0f || threshold >= comp.BreachThreshold)
                continue;

            if (comp.Level < threshold)
            {
                comp.TriggeredIntermediateBreaches.Remove(threshold);
                continue;
            }

            if (comp.TriggeredIntermediateBreaches.Add(threshold))
                TriggerBreach(comp, comp.IntermediateBreachSpawnAmount, intermediate: true, gradual: gradual);
        }

        if (comp.Level < comp.BreachThreshold)
        {
            if (comp.BreachTriggered)
            {
                comp.BreachTriggered = false;
                Dirty(_singleton!.Value, comp);
            }

            return;
        }

        if (comp.BreachTriggered)
            return;

        comp.BreachTriggered = true;
        Dirty(_singleton!.Value, comp);

        TriggerBreach(comp, gradual: gradual);
    }

    private void CheckWave(ZenInterventionComponent comp, bool gradual = false)
    {
        if (comp.Level < comp.MaxLevel)
        {
            if (comp.WaveActive)
            {
                comp.WaveActive = false;
                Dirty(_singleton!.Value, comp);
            }

            return;
        }

        var curTime = _timing.CurTime;

        if (!comp.WaveActive)
        {
            comp.WaveActive = true;
            comp.NextWaveTime = curTime + comp.WaveInterval;
            Dirty(_singleton!.Value, comp);
            SpawnWave(comp, comp.InitialWaveSpawnAmount, gradual);
            return;
        }

        if (curTime < comp.NextWaveTime)
            return;

        comp.NextWaveTime = curTime + comp.WaveInterval;
        Dirty(_singleton!.Value, comp);

        SpawnWave(comp, gradual: gradual);
    }

    public bool StartAmsSurge()
    {
        if (!TryGetComponent(out var comp))
            return false;

        SetLevel(comp.MaxLevel);
        if (!comp.Enabled)
            return false;

        var now = _timing.CurTime;
        var previousCount = _amsSpawns.Count;
        CheckBreach(comp, gradual: true);
        if (comp.WaveActive && comp.NextWaveTime > now && comp.NextWaveTime <= now + AmsSurgeDuration)
            SpawnWave(comp, gradual: true);
        else
            CheckWave(comp, gradual: true);
        if (_amsSpawns.Count == previousCount)
            return false;

        _amsSpawnInterval = AmsSurgeDuration / _amsSpawns.Count;
        _nextAmsSpawn = now + _amsSpawnInterval;
        comp.NextWaveTime = now + AmsSurgeDuration + comp.WaveInterval;
        Dirty(_singleton!.Value, comp);

        _chatSystem.DispatchGlobalAnnouncement(
            Loc.GetString("zen-intervention-announce-ams-surge"),
            sender: Loc.GetString("zen-intervention-announcer-sender"),
            announcementSound: comp.WaveSound,
            colorOverride: comp.AnnouncementColor ?? Color.Red);
        return true;
    }

    private void UpdateAmsSurge(ZenInterventionComponent comp)
    {
        if (_amsSpawns.Count == 0 || _timing.CurTime < _nextAmsSpawn)
            return;

        _nextAmsSpawn += _amsSpawnInterval;
        var prototype = _amsSpawns.Dequeue();
        if (TryGetTargetStation(comp, out var station) && TryGetRandomStationCoordinates(station, comp, out var coords))
            Spawn(prototype, coords);
    }

    private void QueueAmsSpawns(List<EntProtoId> prototypes, int count)
    {
        if (prototypes.Count == 0)
            return;

        for (var i = 0; i < count; i++)
            _amsSpawns.Enqueue(_random.Pick(prototypes));
    }

    public void TriggerBreach(ZenInterventionComponent comp, int? spawnAmount = null, bool intermediate = false, bool gradual = false)
    {
        if (gradual)
        {
            QueueAmsSpawns(comp.BreachMobPrototypes, spawnAmount ?? comp.BreachSpawnAmount);
            return;
        }

        if (!TryGetTargetStation(comp, out var stationUid))
            return;

        for (var i = 0; i < (spawnAmount ?? comp.BreachSpawnAmount); i++)
        {
            if (comp.BreachMobPrototypes.Count == 0)
                break;

            if (!TryGetRandomStationCoordinates(stationUid, comp, out var coords))
                continue;

            var protoId = _random.Pick(comp.BreachMobPrototypes);
            Spawn(protoId, coords);
        }

        _chatSystem.DispatchStationAnnouncement(
            stationUid,
            Loc.GetString(intermediate ? "zen-intervention-announce-minor-breach" : "zen-intervention-announce-breach"),
            Loc.GetString("zen-intervention-announcer-sender"),
            playDefaultSound: !intermediate,
            announcementSound: comp.BreachSound,
            colorOverride: comp.AnnouncementColor);
    }

    public void SpawnWave(ZenInterventionComponent comp, int? spawnAmount = null, bool gradual = false)
    {
        if (gradual)
        {
            QueueAmsSpawns(comp.WaveMobPrototypes, spawnAmount ?? comp.WaveSpawnAmount);
            return;
        }

        if (comp.WaveMobPrototypes.Count == 0 || !TryGetTargetStation(comp, out var stationUid))
            return;

        for (var i = 0; i < (spawnAmount ?? comp.WaveSpawnAmount); i++)
        {
            if (!TryGetRandomStationCoordinates(stationUid, comp, out var coords))
                continue;

            Spawn(_random.Pick(comp.WaveMobPrototypes), coords);
        }

        _chatSystem.DispatchStationAnnouncement(
            stationUid,
            Loc.GetString("zen-intervention-announce-wave"),
            Loc.GetString("zen-intervention-announcer-sender"),
            playDefaultSound: true,
            announcementSound: comp.WaveSound,
            colorOverride: comp.AnnouncementColor);
    }

    private void EnsureSingleton()
    {
        if (_singleton is { } uid && Exists(uid))
            return;

        _singleton = Spawn("ZenInterventionSingletonBlackM", MapCoordinates.Nullspace);

        _pvsOverride.AddGlobalOverride(_singleton.Value);
    }

    private bool TryGetTargetStation(ZenInterventionComponent comp, out EntityUid stationUid)
    {
        var query = EntityQueryEnumerator<StationDataComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            var name = Name(uid);
            if (name.Contains(comp.TargetStationName, StringComparison.OrdinalIgnoreCase))
            {
                stationUid = uid;
                return true;
            }
        }

        stationUid = default;
        return false;
    }

    private bool TryGetRandomStationCoordinates(EntityUid stationUid, ZenInterventionComponent comp, out EntityCoordinates coords)
    {
        coords = default;

        if (!TryComp<StationDataComponent>(stationUid, out var stationData))
            return false;

        var grids = stationData.Grids.ToList();
        if (grids.Count == 0)
            return false;

        for (var attempt = 0; attempt < comp.MaxPlacementAttempts; attempt++)
        {
            var gridUid = _random.Pick(grids);

            if (!TryComp<MapGridComponent>(gridUid, out var grid))
                continue;

            var bounds = grid.LocalAABB;
            if (bounds.IsEmpty())
                continue;

            var randomLocal = new Vector2(
                _random.NextFloat(bounds.Left, bounds.Right),
                _random.NextFloat(bounds.Bottom, bounds.Top));

            var candidate = new EntityCoordinates(gridUid, randomLocal);

            var tileRef = _map.GetTileRef(gridUid, grid, candidate);
            if (tileRef.Tile.IsEmpty)
                continue;

            if (_lookup.AnyEntitiesIntersecting(candidate, LookupFlags.Static))
                continue;

            coords = candidate;
            return true;
        }

        return false;
    }

    public float GetLevel()
    {
        if (_singleton is { } uid && TryComp<ZenInterventionComponent>(uid, out var comp))
            return comp.Level;

        return 0f;
    }

    public void SetLevel(float value)
    {
        EnsureSingleton();

        if (_singleton is not { } uid || !TryComp<ZenInterventionComponent>(uid, out var comp))
            return;

        comp.Level = value < 0f ? 0f : value > comp.MaxLevel ? comp.MaxLevel : value;
        Dirty(uid, comp);
    }

    public void AdjustLevel(float delta)
    {
        EnsureSingleton();

        if (_singleton is not { } uid || !TryComp<ZenInterventionComponent>(uid, out var comp))
            return;

        SetLevel(comp.Level + delta);
    }

    public bool TryGetComponent(out ZenInterventionComponent comp)
    {
        EnsureSingleton();

        if (_singleton is { } uid && TryComp(uid, out comp!))
            return true;

        comp = default!;
        return false;
    }

    public void ForceTriggerBreach()
    {
        if (!TryGetComponent(out var comp))
            return;

        comp.BreachTriggered = true;
        Dirty(_singleton!.Value, comp);

        TriggerBreach(comp);
    }

    public void ForceSpawnWave()
    {
        if (!TryGetComponent(out var comp))
            return;

        SpawnWave(comp);
    }

    public void ResetState()
    {
        if (!TryGetComponent(out var comp))
            return;

        comp.Level = 0f;
        comp.TriggeredIntermediateBreaches.Clear();
        comp.BreachTriggered = false;
        comp.WaveActive = false;
        comp.NextWaveTime = TimeSpan.Zero;
        _amsSpawns.Clear();
        _syncAccumulator = 0f;
        Dirty(_singleton!.Value, comp);
    }
}
