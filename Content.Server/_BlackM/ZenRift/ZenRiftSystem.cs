using System.Linq;
using Content.Shared._BlackM.ZenRift;
using Content.Shared.Examine;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Random.Helpers;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.ZenRift;

public sealed partial class ZenRiftSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedJobSystem _job = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    public static readonly ProtoId<DepartmentPrototype>[] SecurityDepartments = { "Security" };

    private static readonly TimeSpan NavigatorInterval = TimeSpan.FromSeconds(1);

    private TimeSpan _nextNavigatorUpdate;
    private readonly List<ZenRiftMarker> _markers = new();
    private readonly HashSet<EntityUid> _recipients = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZenRiftComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ZenRiftComponent, ExaminedEvent>(OnExamined);

        SubscribeLocalEvent<ZenRiftSpawnedComponent, MobStateChangedEvent>(OnSpawnedMobState);

        SubscribeLocalEvent<ZenRiftNavigatorComponent, PlayerDetachedEvent>(OnNavigatorDetached);
        SubscribeLocalEvent<ZenRiftNavigatorComponent, MobStateChangedEvent>(OnNavigatorMobState);
    }

    #region Public API

    public void Configure(EntityUid uid, EntityUid? rule, int? budget = null, TimeSpan? openDelay = null,
        ZenRiftComponent? rift = null)
    {
        if (!Resolve(uid, ref rift))
            return;

        rift.Rule = rule;
        if (budget != null)
            rift.SpawnBudget = Math.Max(1, budget.Value);

        if (openDelay != null && rift.Phase == ZenRiftPhase.Opening)
        {
            rift.OpenDuration = openDelay.Value;
            SetPhase(uid, rift, ZenRiftPhase.Opening, openDelay.Value);
        }
    }

    public void ForceClose(EntityUid uid, ZenRiftComponent? rift = null)
    {
        if (!Resolve(uid, ref rift, false))
            return;

        Finish(uid, rift);
    }

    public bool IsActiveSecurity(EntityUid mob)
    {
        if (TerminatingOrDeleted(mob) || HasComp<GhostComponent>(mob) || !HasComp<ActorComponent>(mob))
            return false;

        if (_mobState.IsIncapacitated(mob))
            return false;

        if (!_mind.TryGetMind(mob, out var mindId, out var mind) || mind.OwnedEntity != mob)
            return false;

        if (!_job.MindTryGetJobId(mindId, out var job) || job == null)
            return false;

        foreach (var deptId in SecurityDepartments)
        {
            if (_proto.TryIndex(deptId, out var dept) && dept.Roles.Contains(job.Value))
                return true;
        }

        return false;
    }

    public bool IsThreat(EntityUid mob)
    {
        return !TerminatingOrDeleted(mob) && !_mobState.IsIncapacitated(mob);
    }

    #endregion

    #region Events

    private void OnMapInit(Entity<ZenRiftComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.Grid = Transform(ent).GridUid;
        SetPhase(ent, ent.Comp, ZenRiftPhase.Opening, ent.Comp.OpenDuration);
        _audio.PlayPvs(ent.Comp.OpenSound, ent);
    }

    private void OnExamined(Entity<ZenRiftComponent> ent, ref ExaminedEvent args)
    {
        var key = ent.Comp.Phase switch
        {
            ZenRiftPhase.Opening => "zen-rift-examine-opening",
            ZenRiftPhase.Active => "zen-rift-examine-active",
            _ => "zen-rift-examine-closing",
        };

        args.PushMarkup(Loc.GetString(key));
    }

    private void OnSpawnedMobState(Entity<ZenRiftSpawnedComponent> ent, ref MobStateChangedEvent args)
    {
        if (ent.Comp.Neutralized || args.NewMobState == MobState.Alive)
            return;

        ent.Comp.Neutralized = true;

        if (ent.Comp.Rule is { } rule && Exists(rule))
        {
            var ev = new ZenRiftMobNeutralizedEvent(ent);
            RaiseLocalEvent(rule, ref ev);
        }
    }

    private void OnNavigatorDetached(Entity<ZenRiftNavigatorComponent> ent, ref PlayerDetachedEvent args)
    {
        RemCompDeferred<ZenRiftNavigatorComponent>(ent);
    }

    private void OnNavigatorMobState(Entity<ZenRiftNavigatorComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            RemCompDeferred<ZenRiftNavigatorComponent>(ent);
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ZenRiftComponent>();
        while (query.MoveNext(out var uid, out var rift))
        {
            rift.AliveMobs.RemoveWhere(m => !IsThreat(m));

            switch (rift.Phase)
            {
                case ZenRiftPhase.Opening:
                    if (now >= rift.PhaseEnd)
                        Activate(uid, rift, now);
                    break;

                case ZenRiftPhase.Active:
                    if (now >= rift.PhaseEnd)
                    {
                        BeginClosing(uid, rift);
                        break;
                    }

                    if (rift.Spawned < rift.SpawnBudget && now >= rift.NextSpawn)
                    {
                        if (rift.AliveMobs.Count < rift.MaxAlive)
                            SpawnBatch(uid, rift, now);

                        rift.NextSpawn = now + RandomInterval(rift);
                    }
                    break;

                case ZenRiftPhase.Closing:
                    if (now >= rift.PhaseEnd)
                        Finish(uid, rift);
                    break;
            }
        }

        if (now < _nextNavigatorUpdate)
            return;

        _nextNavigatorUpdate = now + NavigatorInterval;
        UpdateNavigators();
    }

    #region Lifecycle

    private void SetPhase(EntityUid uid, ZenRiftComponent rift, ZenRiftPhase phase, TimeSpan duration)
    {
        var now = _timing.CurTime;
        rift.Phase = phase;
        rift.PhaseEnd = now + duration;

        var visuals = EnsureComp<ZenRiftVisualsComponent>(uid);
        visuals.Phase = phase;
        visuals.PhaseStart = now;
        visuals.PhaseDuration = duration;
        Dirty(uid, visuals);
    }

    private void Activate(EntityUid uid, ZenRiftComponent rift, TimeSpan now)
    {
        SetPhase(uid, rift, ZenRiftPhase.Active, rift.ActiveDuration);
        rift.NextSpawn = now;

        if (rift.Rule is { } rule && Exists(rule))
        {
            var ev = new ZenRiftActivatedEvent(uid);
            RaiseLocalEvent(rule, ref ev);
        }
    }

    private void BeginClosing(EntityUid uid, ZenRiftComponent rift)
    {
        if (rift.Phase == ZenRiftPhase.Closing)
            return;

        SetPhase(uid, rift, ZenRiftPhase.Closing, rift.CloseDuration);
        _audio.PlayPvs(rift.CloseSound, uid);
    }

    private void Finish(EntityUid uid, ZenRiftComponent rift)
    {
        if (TerminatingOrDeleted(uid))
            return;

        if (rift.Rule is { } rule && Exists(rule))
        {
            var ev = new ZenRiftClosedEvent(uid, rift.Spawned);
            RaiseLocalEvent(rule, ref ev);
        }

        QueueDel(uid);
    }

    private TimeSpan RandomInterval(ZenRiftComponent rift)
    {
        var min = rift.MinSpawnInterval.TotalSeconds;
        var max = Math.Max(min, rift.MaxSpawnInterval.TotalSeconds);
        return TimeSpan.FromSeconds(_random.NextDouble(min, max));
    }

    private void SpawnBatch(EntityUid uid, ZenRiftComponent rift, TimeSpan now)
    {
        if (rift.Mobs.Count == 0)
            return;

        var count = _random.Next(rift.MinBatch, rift.MaxBatch + 1);
        count = Math.Min(count, rift.SpawnBudget - rift.Spawned);
        count = Math.Min(count, rift.MaxAlive - rift.AliveMobs.Count);
        if (count <= 0)
            return;

        for (var i = 0; i < count; i++)
        {
            var coords = GetSpawnCoordinates(uid, rift);
            var proto = _random.Pick(rift.Mobs);
            var mob = Spawn(proto.Id, coords);

            var tag = EnsureComp<ZenRiftSpawnedComponent>(mob);
            tag.Rift = uid;
            tag.Rule = rift.Rule;

            if (rift.SpawnEffect is { } effect)
                Spawn(effect.Id, coords);

            rift.Spawned++;
            rift.AliveMobs.Add(mob);

            if (rift.Rule is { } rule && Exists(rule))
            {
                var ev = new ZenRiftMobSpawnedEvent(uid, mob);
                RaiseLocalEvent(rule, ref ev);
            }
        }

        _audio.PlayPvs(rift.SpawnSound, uid);

        if (rift.Spawned >= rift.SpawnBudget)
        {
            var lingerEnd = now + rift.LingerDuration;
            if (lingerEnd < rift.PhaseEnd)
                rift.PhaseEnd = lingerEnd;
        }
    }

    private EntityCoordinates GetSpawnCoordinates(EntityUid uid, ZenRiftComponent rift)
    {
        var xform = Transform(uid);
        var fallback = xform.Coordinates;

        if (rift.Grid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid) || xform.GridUid != gridUid)
            return fallback;

        var origin = _map.LocalToTile(gridUid, grid, fallback);
        var r = Math.Max(1, rift.SpawnRadius);

        for (var attempt = 0; attempt < 12; attempt++)
        {
            var offset = new Vector2i(_random.Next(-r, r + 1), _random.Next(-r, r + 1));
            if (offset == Vector2i.Zero)
                continue;

            var tile = origin + offset;
            if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
                continue;

            var candidate = _map.GridTileToLocal(gridUid, grid, tile);
            if (_lookup.AnyEntitiesIntersecting(candidate, LookupFlags.Static))
                continue;

            return candidate;
        }

        return fallback;
    }

    #endregion

    #region Navigator

    private void UpdateNavigators()
    {
        _markers.Clear();
        _recipients.Clear();

        var riftQuery = EntityQueryEnumerator<ZenRiftComponent, TransformComponent>();
        while (riftQuery.MoveNext(out var uid, out var rift, out var xform))
        {
            var map = _transform.GetMapCoordinates(uid, xform);
            if (map.MapId == MapId.Nullspace)
                continue;

            var color = rift.Phase switch
            {
                ZenRiftPhase.Opening => rift.OpeningColor,
                ZenRiftPhase.Active => rift.ActiveColor,
                _ => rift.ClosingColor,
            };

            _markers.Add(new ZenRiftMarker(GetNetEntity(uid), map.Position, map.MapId, color));
        }

        if (_markers.Count > 0)
        {
            var actors = EntityQueryEnumerator<ActorComponent, MobStateComponent>();
            while (actors.MoveNext(out var mob, out _, out _))
            {
                if (!IsActiveSecurity(mob))
                    continue;

                _recipients.Add(mob);
                var nav = EnsureComp<ZenRiftNavigatorComponent>(mob);
                if (nav.Markers.SequenceEqual(_markers))
                    continue;

                nav.Markers = new List<ZenRiftMarker>(_markers);
                Dirty(mob, nav);
            }
        }

        var navs = EntityQueryEnumerator<ZenRiftNavigatorComponent>();
        while (navs.MoveNext(out var uid, out _))
        {
            if (!_recipients.Contains(uid))
                RemCompDeferred<ZenRiftNavigatorComponent>(uid);
        }
    }

    #endregion
}
