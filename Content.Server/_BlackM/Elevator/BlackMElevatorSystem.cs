using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Spawners.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared._BlackM.Elevator;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Camera;
using Content.Shared.Chat;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Tag;
using Content.Shared.Movement.Pulling.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.Elevator;

public sealed class BlackMElevatorSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _recoil = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly SharedPointLightSystem _lights = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawningEvent>(OnPlayerSpawning, before: new[] { typeof(SpawnPointSystem) });
        SubscribeLocalEvent<BlackMElevatorLightComponent, MapInitEvent>(OnLightMapInit);
    }

    private void OnPlayerSpawning(PlayerSpawningEvent ev)
    {
        if (ev.SpawnResult != null)
            return;

        if (ev.Station is not { } station)
            return;

        if (!TryComp(station, out BlackMElevatorStationComponent? cfg))
            return;

        if (ev.Job is not { } job || cfg.BlacklistedJobs.Contains(job))
            return;

        if (!EnsureElevator(station, cfg, out var elevator, out var comp))
            return;

        if (Transform(elevator).GridUid is not { } grid || !TryPickSpawn(grid, out var coords))
        {
            return;
        }

        var mob = _stationSpawning.SpawnPlayerMob(coords, job, ev.HumanoidCharacterProfile, station);
        ev.SpawnResult = mob;

        RegisterPassenger(elevator, comp, mob);
    }

    private bool EnsureElevator(EntityUid station, BlackMElevatorStationComponent cfg,
        out EntityUid elevator, out BlackMElevatorComponent comp)
    {
        elevator = default;
        comp = default!;

        if (cfg.Elevator == null)
        {
            if (cfg.LoadFailed)
                return false;

            var opts = new DeserializationOptions { InitializeMaps = true };
            if (!_mapLoader.TryLoadMap(cfg.ElevatorMap, out var map, out _, opts))
            {
                cfg.LoadFailed = true;
                return false;
            }

            var mapId = map.Value.Comp.MapId;
            cfg.ElevatorMapId = mapId;

            var q = EntityQueryEnumerator<BlackMElevatorComponent, TransformComponent>();
            while (q.MoveNext(out var uid, out var elev, out var xform))
            {
                if (xform.MapID != mapId || elev.Evacuation || elev.Id != cfg.SpawnElevatorId)
                    continue;
                cfg.Elevator = uid;
                break;
            }

            if (cfg.Elevator == null)
            {
                cfg.LoadFailed = true;
                return false;
            }
        }

        elevator = cfg.Elevator.Value;
        return TryComp(elevator, out comp!);
    }

    private bool TryPickSpawn(EntityUid grid, out EntityCoordinates coords)
    {
        var points = new List<EntityCoordinates>();
        var q = EntityQueryEnumerator<BlackMElevatorSpawnPointComponent, TransformComponent>();
        while (q.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid == grid)
                points.Add(xform.Coordinates);
        }

        coords = default;
        if (points.Count == 0)
            return false;

        _random.Shuffle(points);

        foreach (var point in points)
        {
            if (_lookup.GetEntitiesInRange<MobStateComponent>(point, 0.35f).Count == 0)
            {
                coords = point;
                return true;
            }
        }

        coords = points[0];
        return true;
    }

    private void RegisterPassenger(EntityUid uid, BlackMElevatorComponent comp, EntityUid mob)
    {
        var now = _timing.CurTime;

        switch (comp.State)
        {
            case BlackMElevatorState.Idle:
                comp.Passengers.Add(mob);
                comp.State = BlackMElevatorState.Boarding;
                comp.BoardingEnd = now + TimeSpan.FromSeconds(comp.BoardingDelay);
                break;

            case BlackMElevatorState.Boarding:
                comp.Passengers.Add(mob);
                break;

            case BlackMElevatorState.Descending:
                var remaining = comp.DescentTime - (float) (now - comp.StateStart).TotalSeconds;
                if (remaining > comp.JoinCutoff)
                    comp.Passengers.Add(mob);
                else
                    comp.Waiting.Add(mob);
                break;

            default:
                comp.Waiting.Add(mob);
                break;
        }
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var q = EntityQueryEnumerator<BlackMElevatorComponent>();

        while (q.MoveNext(out var uid, out var comp))
        {
            if (comp.Evacuation)
                UpdateEvacuation(uid, comp, now);

            switch (comp.State)
            {
                case BlackMElevatorState.Boarding:
                    if (now >= comp.BoardingEnd)
                    {
                        if (comp.Evacuation)
                            BeginEvacDeparture(uid, comp);
                        else
                            StartDescent(uid, comp);
                    }
                    break;

                case BlackMElevatorState.Descending:
                    UpdateDescent(uid, comp, now);
                    break;

                case BlackMElevatorState.Cooldown:
                    if (now >= comp.StateStart + TimeSpan.FromSeconds(comp.CooldownTime))
                        ResetElevator(uid, comp);
                    break;
            }

            if (comp.LightsRestoreAt is { } restore && now >= restore)
            {
                comp.LightsRestoreAt = null;
                ApplyLights(uid, comp.LightMode);
            }
        }
    }

    private void StartDescent(EntityUid uid, BlackMElevatorComponent comp)
    {
        CleanPassengers(comp);

        if (comp.Passengers.Count == 0)
        {
            comp.State = BlackMElevatorState.Idle;
            return;
        }

        var now = _timing.CurTime;
        comp.State = BlackMElevatorState.Descending;
        comp.StateStart = now;
        comp.Announcements.Sort((a, b) => a.At.CompareTo(b.At));
        comp.HeavyShakes.Sort();
        comp.AnnounceIndex = 0;
        comp.HeavyIndex = 0;
        comp.FadedOut = false;
        comp.NextShake = now + TimeSpan.FromSeconds(2.5);

        SetLightMode(uid, comp, BlackMElevatorLightMode.Dim);

        if (comp.StartSound != null)
            _audio.PlayPvs(comp.StartSound, uid);
        Shake(comp, comp.HeavyShakeStrength);

    }

    private void UpdateDescent(EntityUid uid, BlackMElevatorComponent comp, TimeSpan now)
    {
        var elapsed = (float) (now - comp.StateStart).TotalSeconds;

        while (comp.AnnounceIndex < comp.Announcements.Count
               && elapsed >= comp.Announcements[comp.AnnounceIndex].At)
        {
            var ann = comp.Announcements[comp.AnnounceIndex++];
            Notify(uid, Loc.GetString(ann.Message), ann.Big);
        }

        while (comp.HeavyIndex < comp.HeavyShakes.Count && elapsed >= comp.HeavyShakes[comp.HeavyIndex])
        {
            comp.HeavyIndex++;
            Shake(comp, comp.HeavyShakeStrength);
            FlickerLights(uid, comp, 0.4f, 0.9f);
        }

        if (now >= comp.NextShake)
        {
            comp.NextShake = now + TimeSpan.FromSeconds(_random.NextFloat(2f, 4f));
            Shake(comp, _random.NextFloat(comp.SmallShakeMin, comp.SmallShakeMax));

            if (_random.Prob(0.25f))
                FlickerLights(uid, comp, 0.15f, 0.4f);
        }

        if (comp.LightMode != BlackMElevatorLightMode.Emergency && elapsed >= comp.EmergencyLightsAt)
            SetLightMode(uid, comp, BlackMElevatorLightMode.Emergency);

        if (!comp.FadedOut && elapsed >= comp.DescentTime - comp.FadeOutLead)
        {
            comp.FadedOut = true;
            SendFade(comp.Passengers, true, comp.FadeOutLead, Loc.GetString(comp.FadeText));
        }

        if (elapsed >= comp.DescentTime)
            Arrive(uid, comp);
    }

    private void Arrive(EntityUid uid, BlackMElevatorComponent comp)
    {
        CleanPassengers(comp);

        var dests = new List<EntityUid>();
        var q = EntityQueryEnumerator<BlackMElevatorDestinationComponent>();
        while (q.MoveNext(out var dUid, out var dest))
        {
            if (dest.Id == comp.DestinationId)
                dests.Add(dUid);
        }

        if (dests.Count == 0)
        {
            SendFade(comp.Passengers, false, comp.FadeInTime);
        }
        else
        {
            _random.Shuffle(dests);
            var i = 0;

            foreach (var p in comp.Passengers)
            {
                var dest = dests[i++ % dests.Count];
                ReleaseHolds(p);
                _xform.SetCoordinates(p, Transform(dest).Coordinates);
                RaiseLocalEvent(new BlackMElevatorArrivedEvent(p, comp.Id));
                _recoil.KickCamera(p, _random.NextAngle().ToVec() * 0.5f);
            }

            SendFade(comp.Passengers, false, comp.FadeInTime);
            comp.Passengers.Clear();
        }

        SetLightMode(uid, comp, BlackMElevatorLightMode.Normal);

        comp.State = BlackMElevatorState.Cooldown;
        comp.StateStart = _timing.CurTime;
    }

    private void ResetElevator(EntityUid uid, BlackMElevatorComponent comp)
    {
        comp.Passengers.UnionWith(comp.Waiting);
        comp.Waiting.Clear();
        CleanPassengers(comp);

        SetLightMode(uid, comp, BlackMElevatorLightMode.Normal);

        if (comp.Passengers.Count > 0)
        {
            comp.State = BlackMElevatorState.Boarding;
            comp.BoardingEnd = _timing.CurTime + TimeSpan.FromSeconds(comp.BoardingDelay);
        }
        else
        {
            comp.State = BlackMElevatorState.Idle;
        }

    }

    public bool SetEvacuation(string elevatorId, bool enabled)
    {
        var q = EntityQueryEnumerator<BlackMElevatorComponent>();
        while (q.MoveNext(out _, out var comp))
        {
            if (!comp.Evacuation || comp.Id != elevatorId)
                continue;

            comp.Enabled = enabled;
            return true;
        }

        return false;
    }

    private void UpdateEvacuation(EntityUid uid, BlackMElevatorComponent comp, TimeSpan now)
    {
        switch (comp.State)
        {
            case BlackMElevatorState.Idle:
                if (!comp.Enabled)
                    return;

                comp.State = BlackMElevatorState.Boarding;
                comp.BoardingEnd = now + TimeSpan.FromSeconds(comp.BoardingDelay);
                comp.BoardingWarned = false;
                var boarding = Loc.GetString(comp.BoardingMessage, ("seconds", (int) comp.BoardingDelay));
                if (comp.GlobalAnnounce)
                    AnnounceGlobal(Loc.GetString(comp.Sender), boarding, Color.Cyan);
                SayAtBoarding(comp.Id, boarding);
                break;

            case BlackMElevatorState.Boarding:
                if (!comp.Enabled)
                {
                    comp.State = BlackMElevatorState.Idle;
                    return;
                }

                if (!comp.BoardingWarned && comp.BoardingEnd - now <= TimeSpan.FromSeconds(10))
                {
                    comp.BoardingWarned = true;
                    SayAtBoarding(comp.Id, Loc.GetString(comp.ClosingMessage));
                }
                break;
        }
    }

    private void BeginEvacDeparture(EntityUid uid, BlackMElevatorComponent comp)
    {
        var grid = Transform(uid).GridUid;
        var boarded = CollectEvacPassengers(comp.Id);

        if (boarded.Count == 0 || grid == null)
        {
            comp.BoardingEnd = _timing.CurTime + TimeSpan.FromSeconds(comp.BoardingDelay);
            comp.BoardingWarned = true;
            return;
        }

        SayAtBoarding(comp.Id, Loc.GetString(comp.DepartingMessage));

        foreach (var p in boarded)
        {
            ReleaseHolds(p);

            if (TryPickSpawn(grid.Value, out var coords))
                _xform.SetCoordinates(p, coords);

            comp.Passengers.Add(p);
        }

        StartDescent(uid, comp);
    }

    private HashSet<EntityUid> CollectEvacPassengers(string elevatorId)
    {
        var result = new HashSet<EntityUid>();
        var q = EntityQueryEnumerator<BlackMElevatorBoardZoneComponent, TransformComponent>();

        while (q.MoveNext(out _, out var zone, out var xform))
        {
            if (zone.ElevatorId != elevatorId)
                continue;

            foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(xform.Coordinates, zone.Range))
            {
                if (_mobState.IsDead(ent.Owner) || !HasComp<ActorComponent>(ent.Owner))
                    continue;

                if (zone.RestrictedTags.Count > 0 && _tag.HasAnyTag(ent.Owner, zone.RestrictedTags))
                    continue;

                result.Add(ent.Owner);
            }
        }

        return result;
    }

    private void AnnounceGlobal(string sender, string message, Color color)
    {
        _chatSystem.DispatchGlobalAnnouncement(
            message,
            sender: sender,
            playSound: true,
            colorOverride: color);
    }

    private void Shake(BlackMElevatorComponent comp, float strength)
    {
        foreach (var p in comp.Passengers)
        {
            _recoil.KickCamera(p, _random.NextAngle().ToVec() * strength);
        }
    }

    private void Notify(EntityUid elevator, string text, bool big)
    {
        if (!TryGetDispatcher(elevator, out var dispatcher))
            return;

        if (big)
            text = text.ToUpperInvariant();

        _chatSystem.TrySendInGameICMessage(dispatcher, text, InGameICChatType.Speak,
            hideChat: false, ignoreActionBlocker: true);
    }

    private bool TryGetDispatcher(EntityUid elevator, out EntityUid dispatcher)
    {
        var grid = Transform(elevator).GridUid;
        var q = EntityQueryEnumerator<BlackMElevatorDispatcherComponent, TransformComponent>();
        while (q.MoveNext(out var uid, out var disp, out var xform))
        {
            if (xform.GridUid != grid || disp.ElevatorId != null)
                continue;
            dispatcher = uid;
            return true;
        }

        dispatcher = default;
        return false;
    }

    private void SayAtBoarding(string elevatorId, string text)
    {
        var q = EntityQueryEnumerator<BlackMElevatorDispatcherComponent>();
        while (q.MoveNext(out var uid, out var disp))
        {
            if (disp.ElevatorId != elevatorId)
                continue;

            _chatSystem.TrySendInGameICMessage(uid, text, InGameICChatType.Speak,
                hideChat: false, ignoreActionBlocker: true);
        }
    }

    private void SendFade(IEnumerable<EntityUid> targets, bool toBlack, float duration, string? text = null)
    {
        var ev = new BlackMElevatorFadeEvent(toBlack, duration, text);
        foreach (var t in targets)
        {
            if (TryComp(t, out ActorComponent? actor))
                RaiseNetworkEvent(ev, actor.PlayerSession);
        }
    }

    private void OnLightMapInit(Entity<BlackMElevatorLightComponent> ent, ref MapInitEvent args)
    {
        if (!_lights.TryGetLight(ent, out var light))
            return;

        ent.Comp.BaseEnergy = light.Energy;
        ent.Comp.BaseColor = light.Color;
    }

    private void SetLightMode(EntityUid uid, BlackMElevatorComponent comp, BlackMElevatorLightMode mode)
    {
        comp.LightMode = mode;
        ApplyLights(uid, mode);
    }

    private void ApplyLights(EntityUid elevator, BlackMElevatorLightMode mode)
    {
        var grid = Transform(elevator).GridUid;
        var q = EntityQueryEnumerator<BlackMElevatorLightComponent, TransformComponent>();

        while (q.MoveNext(out var uid, out var light, out var xform))
        {
            if (xform.GridUid != grid)
                continue;

            _lights.SetEnabled(uid, true);

            switch (mode)
            {
                case BlackMElevatorLightMode.Normal:
                    _lights.SetEnergy(uid, light.BaseEnergy);
                    _lights.SetColor(uid, light.BaseColor);
                    break;
                case BlackMElevatorLightMode.Dim:
                    _lights.SetEnergy(uid, light.BaseEnergy * 0.45f);
                    _lights.SetColor(uid, light.BaseColor);
                    break;
                case BlackMElevatorLightMode.Emergency:
                    _lights.SetEnergy(uid, light.BaseEnergy * 0.8f);
                    _lights.SetColor(uid, Color.FromHex("#ff2a1a"));
                    break;
            }
        }
    }

    private void FlickerLights(EntityUid elevator, BlackMElevatorComponent comp, float min, float max)
    {
        var grid = Transform(elevator).GridUid;
        var q = EntityQueryEnumerator<BlackMElevatorLightComponent, TransformComponent>();

        while (q.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid == grid)
                _lights.SetEnabled(uid, false);
        }

        comp.LightsRestoreAt = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(min, max));
    }

    private void ReleaseHolds(EntityUid p)
    {
        _pulling.StopAllPulls(p);

        if (TryComp(p, out BuckleComponent? buckle) && buckle.Buckled)
            _buckle.Unbuckle((p, buckle), null);
    }

    private void CleanPassengers(BlackMElevatorComponent comp)
    {
        comp.Passengers.RemoveWhere(p => TerminatingOrDeleted(p));
        comp.Waiting.RemoveWhere(p => TerminatingOrDeleted(p));
    }
}