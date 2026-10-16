using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server._BlackM.Elevator;
using Content.Server._BlackM.ZenIntervention;
using Content.Server.Chat.Systems;
using Content.Server.Decals;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Light.Components;
using Content.Server.Light.EntitySystems;
using Content.Server.RoundEnd;
using Content.Shared._BlackM.Ams;
using Content.Shared.Audio;
using Content.Shared.Camera;
using Content.Shared.Containers.ItemSlots;
using Content.Server._BlackM.Achievements;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Light.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.Ams;

public sealed class AmsLaunchedEvent : EntityEventArgs
{
}

public sealed class AmsDetonatedEvent : EntityEventArgs
{
}

public sealed class AmsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;
    [Dependency] private readonly PoweredLightSystem _poweredLight = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly BlackMElevatorSystem _elevator = default!;
    [Dependency] private readonly SharedDoorSystem _door = default!;
    [Dependency] private readonly ZenInterventionSystem _zenIntervention = default!;
    [Dependency] private readonly DecalSystem _decals = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _recoil = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly AmsRoundEndSystem _amsRoundEnd = default!;
    [Dependency] private readonly AmsAchievementsSystem _amsAchievements = default!;

    private readonly Dictionary<(EntityUid Machine, EntityUid Part), EntityUid> _partInsertActors = new();

    private const string EvacuationElevatorId = "evac";
    private static readonly TimeSpan UiInterval = TimeSpan.FromMilliseconds(500);

    private static readonly SoundPathSpecifier HumIdle = new("/Audio/_BlackM/Ams/ams_hum_idle.ogg");
    private static readonly SoundPathSpecifier HumSync = new("/Audio/_BlackM/Ams/ams_hum_sync.ogg");
    private static readonly SoundPathSpecifier HumReady = new("/Audio/_BlackM/Ams/ams_hum_ready.ogg");
    private static readonly SoundPathSpecifier HumActive = new("/Audio/_BlackM/Ams/ams_hum_active.ogg");
    private static readonly SoundPathSpecifier HumCritical = new("/Audio/_BlackM/Ams/ams_hum_critical.ogg");
    private static readonly SoundPathSpecifier PowerUpSound = new("/Audio/_BlackM/Ams/ams_powerup.ogg");
    private static readonly SoundPathSpecifier ReadySound = new("/Audio/_BlackM/Ams/ams_calibrated.ogg");
    private static readonly SoundPathSpecifier InfectSound = new("/Audio/_BlackM/Ams/ams_infect.ogg");
    private static readonly SoundPathSpecifier CrackleSound = new("/Audio/_BlackM/Ams/ams_crackle.ogg");
    private static readonly SoundPathSpecifier ChargeSound = new("/Audio/_BlackM/Ams/ams_charge.ogg");
    private static readonly SoundPathSpecifier CascadeSound = new("/Audio/_BlackM/Ams/ams_cascade.ogg");
    private static readonly SoundPathSpecifier ExplosionSound = new("/Audio/_BlackM/Ams/ams_explosion.ogg");

    private static readonly SoundPathSpecifier SyncAnnounceSound = new("/Audio/_BlackM/Announcements/announcesync.ogg");
    private static readonly SoundPathSpecifier AnnounceSound = new("/Audio/_BlackM/Announcements/announceams.ogg");
    private static readonly SoundPathSpecifier HecuSound = new("/Audio/_BlackM/Announcements/hecucrack.ogg");

    private readonly record struct Look(SoundPathSpecifier Sound, float Volume, float Range, string Color, float Energy, float Radius);

    private static readonly Dictionary<AmsStatus, Look> Looks = new()
    {
        [AmsStatus.Inactive]      = new(HumIdle,     -10f,  4f, "#3ad1ff", 1.5f, 3f),
        [AmsStatus.Synchronizing] = new(HumSync,      -6f,  8f, "#3affb0", 2.5f, 5f),
        [AmsStatus.Ready]         = new(HumReady,     -5f, 10f, "#8dfff0", 3.5f, 6f),
        [AmsStatus.Active]        = new(HumActive,    -3f, 14f, "#9dff4a", 5f,   8f),
        [AmsStatus.Detonating]    = new(HumCritical,   0f, 18f, "#ff3b30", 6f,  10f),
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<AmsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AmsComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<AmsComponent, EntInsertedIntoContainerMessage>(OnPartInserted);
        SubscribeLocalEvent<AmsComponent, ItemSlotInsertAttemptEvent>(OnPartInsertAttempt);
        SubscribeLocalEvent<AmsComponent, EntRemovedFromContainerMessage>(OnPartRemoved);
        SubscribeLocalEvent<AmsComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<AmsComponent, AmsLaunchButtonMessage>(OnLaunchButton);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _openedRandomDoors.Clear();
        _savedLightState.Clear();
        _savedEmergencyColors.Clear();
        _infections.Clear();
        _sequences.Clear();
    }

    private void OnMapInit(EntityUid uid, AmsComponent ams, MapInitEvent args)
    {
        ApplyStatusEffects(uid, ams);
    }

    private void OnShutdown(EntityUid uid, AmsComponent ams, ComponentShutdown args)
    {
        _infections.Remove(uid);
        _sequences.Remove(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<AmsComponent>();
        while (query.MoveNext(out var uid, out var ams))
        {
            UpdateStatus(uid, ams);
            UpdateFlicker(uid, ams);
            UpdateUi(uid, ams);
        }

        UpdateInfections(frameTime);
        UpdateSequences();
    }

    #region status

    public void SetStatus(EntityUid uid, AmsComponent ams, AmsStatus status)
    {
        if (ams.Status == status)
            return;

        ams.Status = status;
        Dirty(uid, ams);
        ApplyStatusEffects(uid, ams);

        switch (status)
        {
            case AmsStatus.Synchronizing:
                StartInfection(uid, ams);
                break;
            case AmsStatus.Detonating:
                StartSequence(uid);
                break;
            case AmsStatus.Destroyed:
                _infections.Remove(uid);
                break;
        }
    }

    private void UpdateStatus(EntityUid uid, AmsComponent ams)
    {
        var now = _timing.CurTime;

        switch (ams.Status)
        {
            case AmsStatus.Synchronizing:
                if (ams.SyncStartTime is not { } start)
                    break;

                var elapsed = (float) (now - start).TotalSeconds;

                if (!ams.HecuSpawned && elapsed >= ams.HecuSpawnDelay)
                {
                    ams.HecuSpawned = true;
                    SpawnHecuSoldiers();
                    SendHecuAnnouncement();
                    OpenInfectedMaintDoors();
                }

                if (elapsed >= ams.ChargeTime)
                {
                    SetStatus(uid, ams, AmsStatus.Ready);
                    Announce(Loc.GetString("ams-announce-sync-ready"), SyncAnnounceSound, Color.Gray);
                    _audio.PlayPvs(ReadySound, uid, AudioParams.Default.WithMaxDistance(14f));
                    RestoreLights();
                }

                break;

            case AmsStatus.Active:
                if (ams.ActiveEndTime is not { } end)
                    break;

                var remaining = end - now;

                if (!ams.ClosingWarningSent && remaining <= TimeSpan.FromSeconds(15))
                {
                    ams.ClosingWarningSent = true;
                    Announce(Loc.GetString("ams-announce-closing-soon"), AnnounceSound, Color.Gray);
                }

                if (remaining <= TimeSpan.Zero)
                    BeginDetonation(uid, ams);

                break;
        }
    }

    private void OnLaunchButton(EntityUid uid, AmsComponent ams, AmsLaunchButtonMessage args)
    {
        switch (ams.Status)
        {
            case AmsStatus.Inactive:
                if (CountFilled(uid, ams) < ams.PartSlots.Count)
                    return;

                ams.SyncStartTime = _timing.CurTime;
                ams.HecuSpawned = false;
                SetStatus(uid, ams, AmsStatus.Synchronizing);
                Announce(Loc.GetString("ams-announce-sync-started"), SyncAnnounceSound, Color.Gray);
                _audio.PlayPvs(PowerUpSound, uid, AudioParams.Default.WithMaxDistance(16f));
                TurnOffAllLights(uid);
                break;

            case AmsStatus.Ready:
                ams.ActiveEndTime = _timing.CurTime + TimeSpan.FromSeconds(ams.ActiveDuration);
                ams.ClosingWarningSent = false;
                SetStatus(uid, ams, AmsStatus.Active);
                Announce(Loc.GetString("ams-announce-launched"), AnnounceSound, Color.Gray);

                if (_elevator.SetEvacuation(EvacuationElevatorId, true))
                    Announce(Loc.GetString("ams-announce-evac-available"), AnnounceSound, Color.Gray);

                RaiseLocalEvent(new AmsLaunchedEvent());
                break;
        }
    }

    private void BeginDetonation(EntityUid uid, AmsComponent ams)
    {
        _elevator.SetEvacuation(EvacuationElevatorId, false);

        ams.ActiveEndTime = null;
        ams.DetonationTime = _timing.CurTime + TimeSpan.FromSeconds(ams.DetonationDelay);
        SetStatus(uid, ams, AmsStatus.Detonating);

        Announce(Loc.GetString("ams-announce-cascade-start", ("seconds", (int) ams.DetonationDelay)),
            AnnounceSound, Color.Red);
    }

    #endregion

    #region parts

    private void OnPartInsertAttempt(EntityUid uid, AmsComponent ams, ref ItemSlotInsertAttemptEvent args)
    {
        if (ams.Status != AmsStatus.Inactive || args.Slot.ID is not { } slotId ||
            !ams.PartSlots.Contains(slotId) || args.User is not { } actor)
            return;

        _partInsertActors[(uid, args.Item)] = actor;
    }

    private void OnPartRemoved(EntityUid uid, AmsComponent ams, EntRemovedFromContainerMessage args)
    {
        _partInsertActors.Remove((uid, args.Entity));
    }

    private int CountFilled(EntityUid uid, AmsComponent ams)
    {
        var count = 0;
        foreach (var id in ams.PartSlots)
        {
            if (_itemSlots.TryGetSlot(uid, id, out var slot) && slot.HasItem)
                count++;
        }

        return count;
    }

    private void OnPartInserted(EntityUid uid, AmsComponent ams, EntInsertedIntoContainerMessage args)
    {
        var slotId = args.Container.ID;
        if (ams.Status != AmsStatus.Inactive || !ams.PartSlots.Contains(slotId))
            return;

        if (_partInsertActors.Remove((uid, args.Entity), out var actor) &&
            HasComp<AmsPartComponent>(args.Entity))
        {
            _amsAchievements.OnPartInstalled(actor, args.Entity);
        }

        if (_itemSlots.TryGetSlot(uid, slotId, out var slot))
            _itemSlots.SetLock(uid, slot, true);

        _zenIntervention.AdjustLevel(-ams.ZenReductionPerPart);
        OpenRandomPoolDoor();
        OpenLootRoomForSlot(slotId);

        if (!ams.SpecialDoorOpened && CountFilled(uid, ams) >= ams.SpecialDoorRequiredParts)
        {
            ams.SpecialDoorOpened = true;
            OpenSpecialDoor(ams);
        }

        PushUi(uid, ams);
    }

    #endregion

    #region UI

    private void OnUiOpened(EntityUid uid, AmsComponent ams, BoundUIOpenedEvent args)
    {
        PushUi(uid, ams);
    }

    private void UpdateUi(EntityUid uid, AmsComponent ams)
    {
        var now = _timing.CurTime;
        if (now < ams.NextUiUpdate)
            return;

        ams.NextUiUpdate = now + UiInterval;

        if (_ui.IsUiOpen(uid, AmsUiKey.Key))
            PushUi(uid, ams);
    }

    private void PushUi(EntityUid uid, AmsComponent ams)
    {
        var filled = new List<string>();
        foreach (var id in ams.PartSlots)
        {
            if (_itemSlots.TryGetSlot(uid, id, out var slot) && slot.HasItem)
                filled.Add(id);
        }

        _ui.SetUiState(uid, AmsUiKey.Key,
            new AmsBuiState(ams.Status, ams.SyncStartTime, ams.ActiveEndTime, ams.DetonationTime,
                ams.ChargeTime, new List<string>(ams.PartSlots), filled));
    }

    #endregion

    #region Ambient

    private void ApplyStatusEffects(EntityUid uid, AmsComponent ams)
    {
        if (Looks.TryGetValue(ams.Status, out var look))
        {
            _ambient.SetSound(uid, look.Sound);
            _ambient.SetVolume(uid, look.Volume);
            _ambient.SetRange(uid, look.Range);
            _ambient.SetAmbience(uid, true);

            _pointLight.SetEnabled(uid, true);
            _pointLight.SetColor(uid, Color.FromHex(look.Color));
            _pointLight.SetEnergy(uid, look.Energy);
            _pointLight.SetRadius(uid, look.Radius);
        }
        else
        {
            _ambient.SetAmbience(uid, false);
            _pointLight.SetEnabled(uid, false);
        }

        var wantRift = ams.Status is AmsStatus.Active or AmsStatus.Detonating;
        if (wantRift && ams.RiftEffect == null)
        {
            ams.RiftEffect = Spawn("AmsEffectRift", Transform(uid).Coordinates);
        }
        else if (!wantRift && ams.RiftEffect is { } rift)
        {
            QueueDel(rift);
            ams.RiftEffect = null;
        }
    }

    private void UpdateFlicker(EntityUid uid, AmsComponent ams)
    {
        if (ams.Status != AmsStatus.Detonating)
            return;

        var now = _timing.CurTime;
        if (now < ams.NextFlicker)
            return;

        ams.NextFlicker = now + TimeSpan.FromSeconds(_random.NextFloat(0.08f, 0.3f));
        _pointLight.SetEnergy(uid, _random.NextFloat(2f, 9f));
        _pointLight.SetColor(uid, _random.Prob(0.25f) ? Color.White : Color.FromHex("#ff3b30"));
    }

    #endregion

    #region door hecu light

    private const string InfectedMaintDoorPrototype = "AirlockMaintInfectedBlackM";
    private const string RandomDoorGroup = "ams_random";
    private const string SpecialDoorGroup = "ams_special";
    private const string LootDoorGroupPrefix = "ams_loot_";

    private readonly HashSet<EntityUid> _openedRandomDoors = new();
    private readonly Dictionary<EntityUid, bool> _savedLightState = new();
    private readonly Dictionary<EntityUid, Color> _savedEmergencyColors = new();

    private void SpawnHecuSoldiers()
    {
        var query = EntityQueryEnumerator<HecuSpawnPointComponent>();
        while (query.MoveNext(out var spawnUid, out _))
        {
            Spawn("RandomHumanoidHECUSoldierSpawner", Transform(spawnUid).Coordinates);
        }
    }

    private void SendHecuAnnouncement()
    {
        _chat.DispatchGlobalAnnouncement(
            Loc.GetString("ams-announce-hecu-intercept"),
            sender: Loc.GetString("ams-announce-hecu-sender"),
            playSound: true,
            announcementSound: HecuSound,
            colorOverride: Color.DarkGreen);
    }

    private void OpenInfectedMaintDoors()
    {
        var query = EntityQueryEnumerator<DoorComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var door, out var meta))
        {
            if (meta.EntityPrototype?.ID != InfectedMaintDoorPrototype)
                continue;

            _door.StartOpening(uid, door);

            if (TryComp<DoorBoltComponent>(uid, out var bolt))
                _door.SetBoltsDown((uid, bolt), true, null, false);
        }
    }

    private void OpenRandomPoolDoor()
    {
        var candidates = new List<EntityUid>();
        var query = EntityQueryEnumerator<AmsControlledDoorComponent>();
        while (query.MoveNext(out var doorUid, out var tag))
        {
            if (tag.GroupId == RandomDoorGroup && !_openedRandomDoors.Contains(doorUid))
                candidates.Add(doorUid);
        }

        if (candidates.Count == 0)
            return;

        var chosen = _random.Pick(candidates);
        _openedRandomDoors.Add(chosen);
        OpenDoor(chosen);
        Announce(Loc.GetString("ams-announce-random-door"), AnnounceSound, Color.Orange);
    }

    private void OpenSpecialDoor(AmsComponent ams)
    {
        var query = EntityQueryEnumerator<AmsControlledDoorComponent>();
        while (query.MoveNext(out var doorUid, out var tag))
        {
            if (tag.GroupId != SpecialDoorGroup)
                continue;

            OpenDoor(doorUid);
            Announce(Loc.GetString("ams-announce-special-door",
                ("count", ams.SpecialDoorRequiredParts), ("total", ams.PartSlots.Count)), AnnounceSound, Color.Orange);
            return;
        }
    }

    private void OpenLootRoomForSlot(string slotId)
    {
        var groupId = LootDoorGroupPrefix + slotId;

        var query = EntityQueryEnumerator<AmsControlledDoorComponent>();
        while (query.MoveNext(out var doorUid, out var tag))
        {
            if (tag.GroupId != groupId)
                continue;

            OpenDoor(doorUid);

            var location = string.IsNullOrWhiteSpace(tag.AnnouncementLocationName) ? slotId : tag.AnnouncementLocationName;
            Announce(Loc.GetString("ams-announce-loot-room", ("location", location)), AnnounceSound, Color.Orange);
            return;
        }
    }

    private void OpenDoor(EntityUid uid)
    {
        if (TryComp<DoorBoltComponent>(uid, out var bolt))
            _door.SetBoltsDown((uid, bolt), false, null, false);

        if (TryComp<DoorComponent>(uid, out var door))
            _door.StartOpening(uid, door);
    }

    private void TurnOffAllLights(EntityUid portalUid)
    {
        _savedLightState.Clear();
        _savedEmergencyColors.Clear();

        var grid = Transform(portalUid).GridUid;
        if (grid == null)
            return;

        var lamps = EntityQueryEnumerator<PoweredLightComponent>();
        while (lamps.MoveNext(out var uid, out var lamp))
        {
            if (Transform(uid).GridUid != grid || HasComp<EmergencyLightComponent>(uid))
                continue;

            _savedLightState[uid] = lamp.On;

            if (lamp.On)
                _poweredLight.SetState(uid, false, lamp);
        }

        var emergency = EntityQueryEnumerator<EmergencyLightComponent, PointLightComponent>();
        while (emergency.MoveNext(out var eUid, out _, out var eLight))
        {
            if (Transform(eUid).GridUid != grid)
                continue;

            _savedEmergencyColors[eUid] = eLight.Color;
            _pointLight.SetEnabled(eUid, true, eLight);
            _pointLight.SetColor(eUid, Color.Red, eLight);
        }
    }

    private void RestoreLights()
    {
        var lamps = EntityQueryEnumerator<PoweredLightComponent>();
        while (lamps.MoveNext(out var uid, out var lamp))
        {
            if (!_savedLightState.TryGetValue(uid, out var wasOn))
                continue;

            if (wasOn && !lamp.On)
                _poweredLight.SetState(uid, true, lamp);
            else if (!wasOn && lamp.On)
                _poweredLight.SetState(uid, false, lamp);
        }

        var emergency = EntityQueryEnumerator<EmergencyLightComponent, PointLightComponent>();
        while (emergency.MoveNext(out var eUid, out _, out var eLight))
        {
            if (_savedEmergencyColors.TryGetValue(eUid, out var color))
                _pointLight.SetColor(eUid, color, eLight);
        }

        _savedLightState.Clear();
        _savedEmergencyColors.Clear();
    }

    private void Announce(string message, SoundPathSpecifier sound, Color color)
    {
        _chat.DispatchGlobalAnnouncement(
            message,
            sender: Loc.GetString("ams-announce-sender"),
            playSound: true,
            announcementSound: sound,
            colorOverride: color);
    }

    #endregion

    #region infect

    private sealed class Infection
    {
        public EntityUid Grid;
        public readonly List<Vector2i> Frontier = new();
        public readonly HashSet<Vector2i> Seen = new();
        public float Accumulator;
    }

    private const int InfectionFrontierWindow = 24;
    private const int InfectionMaxPerTick = 40;
    private static readonly (int X, int Y)[] Neighbors = { (1, 0), (-1, 0), (0, 1), (0, -1) };

    private readonly Dictionary<EntityUid, Infection> _infections = new();

    private void StartInfection(EntityUid uid, AmsComponent ams)
    {
        if (_infections.ContainsKey(uid) || ams.InfectionDecals.Count == 0)
            return;

        var xform = Transform(uid);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var origin = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var state = new Infection { Grid = gridUid };
        state.Frontier.Add(origin);
        state.Seen.Add(origin);
        _infections[uid] = state;
    }

    private void UpdateInfections(float frameTime)
    {
        if (_infections.Count == 0)
            return;

        var now = _timing.CurTime;

        foreach (var (uid, state) in _infections)
        {
            if (!TryComp<AmsComponent>(uid, out var ams) || !TryComp<MapGridComponent>(state.Grid, out var grid))
                continue;

            var rate = ams.Status switch
            {
                AmsStatus.Synchronizing => MathHelper.Lerp(ams.InfectionStartRate, ams.InfectionEndRate,
                    AmsHelpers.GetCharge(ams.Status, ams.SyncStartTime, ams.ChargeTime, now)),
                AmsStatus.Ready or AmsStatus.Active => ams.InfectionEndRate,
                AmsStatus.Detonating => ams.InfectionEndRate * 1.5f,
                _ => 0f,
            };

            state.Accumulator += rate * frameTime;
            var count = Math.Min((int) state.Accumulator, InfectionMaxPerTick);
            state.Accumulator -= (int) state.Accumulator;

            for (var i = 0; i < count && state.Frontier.Count > 0; i++)
                InfectStep(state, ams, grid);
        }
    }

    private void InfectStep(Infection state, AmsComponent ams, MapGridComponent grid)
    {
        var idx = _random.Next(Math.Min(state.Frontier.Count, InfectionFrontierWindow));
        var pos = state.Frontier[idx];
        state.Frontier.RemoveAt(idx);

        if (_map.GetTileRef(state.Grid, grid, pos).Tile.IsEmpty)
            return;

        var coords = new EntityCoordinates(state.Grid, new Vector2(pos.X * grid.TileSize, pos.Y * grid.TileSize));
        _decals.TryAddDecal(_random.Pick(ams.InfectionDecals), coords, out _,
            rotation: Angle.FromDegrees(90 * _random.Next(4)));

        if (_random.Prob(0.015f))
            _audio.PlayPvs(InfectSound, _map.GridTileToLocal(state.Grid, grid, pos),
                AudioParams.Default.WithVolume(-4f).WithMaxDistance(10f));

        foreach (var (dx, dy) in Neighbors)
        {
            var next = new Vector2i(pos.X + dx, pos.Y + dy);
            if (state.Seen.Add(next))
                state.Frontier.Add(next);
        }
    }

    #endregion

    #region nuke

    private sealed class Sequence
    {
        public EntityUid Grid;
        public bool HasGrid;
        public MapId Map;
        public MapCoordinates Epicenter;
        public EntityCoordinates EpicenterLocal;

        public TimeSpan NextPulse;
        public TimeSpan NextWave;
        public bool Blasting;
        public bool Warned30, Warned10, ChargePlayed;

        public int Wave;
        public float MaxDist;
        public List<Vector2i>[] Buckets = Array.Empty<List<Vector2i>>();
        public List<(EntityUid Light, float Dist)> Lights = new();
    }

    private readonly Dictionary<EntityUid, Sequence> _sequences = new();

    private void StartSequence(EntityUid uid)
    {
        if (_sequences.ContainsKey(uid))
            return;

        var xform = Transform(uid);
        var seq = new Sequence
        {
            Map = xform.MapID,
            Epicenter = _transform.GetMapCoordinates(uid, xform),
            EpicenterLocal = xform.Coordinates,
            NextPulse = _timing.CurTime,
        };

        if (xform.GridUid is { } grid && HasComp<MapGridComponent>(grid))
        {
            seq.Grid = grid;
            seq.HasGrid = true;
        }

        _sequences[uid] = seq;
    }

    private void UpdateSequences()
    {
        if (_sequences.Count == 0)
            return;

        var now = _timing.CurTime;

        foreach (var (uid, seq) in _sequences.ToArray())
        {
            if (!TryComp<AmsComponent>(uid, out var ams))
            {
                _sequences.Remove(uid);
                continue;
            }

            if (!seq.Blasting)
                UpdateCountdown(uid, ams, seq, now);
            else if (now >= seq.NextWave)
                DoWave(uid, ams, seq, now);
        }
    }

    private void UpdateCountdown(EntityUid uid, AmsComponent ams, Sequence seq, TimeSpan now)
    {
        var remaining = (float) ((ams.DetonationTime ?? now) - now).TotalSeconds;
        var progress = 1f - Math.Clamp(remaining / MathF.Max(ams.DetonationDelay, 1f), 0f, 1f);

        if (!seq.Warned30 && remaining <= 30f)
        {
            seq.Warned30 = true;
            Announce(Loc.GetString("ams-announce-cascade-30"), AnnounceSound, Color.OrangeRed);
        }

        if (!seq.Warned10 && remaining <= 10f)
        {
            seq.Warned10 = true;
            Announce(Loc.GetString("ams-announce-cascade-10"), AnnounceSound, Color.Red);
        }

        if (!seq.ChargePlayed && remaining <= 12f)
        {
            seq.ChargePlayed = true;
            _audio.PlayGlobal(ChargeSound, Filter.BroadcastMap(seq.Map), false, AudioParams.Default.WithVolume(2f));
        }

        if (now >= seq.NextPulse)
        {
            Kick(seq, 0.15f + 0.6f * progress);

            var radius = 1.5f + 8f * progress;
            var offset = _random.NextAngle().ToVec() * _random.NextFloat(0.4f, 1f) * radius;
            var coords = seq.EpicenterLocal.Offset(offset);

            Spawn("AmsEffectArc", coords);
            if (_random.Prob(0.5f))
                _audio.PlayPvs(CrackleSound, coords, AudioParams.Default.WithMaxDistance(14f));

            seq.NextPulse = now + TimeSpan.FromSeconds(MathHelper.Lerp(2.0f, 0.35f, progress));
        }

        if (remaining <= 0f)
            StartBlast(uid, ams, seq, now);
    }

    private void StartBlast(EntityUid uid, AmsComponent ams, Sequence seq, TimeSpan now)
    {
        seq.Blasting = true;

        SetStatus(uid, ams, AmsStatus.Destroyed);
        RaiseLocalEvent(new AmsDetonatedEvent());

        _audio.PlayGlobal(ExplosionSound, Filter.BroadcastMap(seq.Map), false, AudioParams.Default.WithVolume(4f));

        _explosion.QueueExplosion(seq.Epicenter, ams.ExplosionType, ams.CoreIntensity, ams.CoreSlope,
            ams.CoreMaxTileIntensity, null);

        Spawn("AmsEffectShockwaveHuge", seq.EpicenterLocal);
        Spawn("AmsEffectBlastFlash", seq.EpicenterLocal);
        Kick(seq, 3.5f);

        if (seq.HasGrid)
            PrepareWaves(ams, seq);

        seq.NextWave = now + TimeSpan.FromSeconds(seq.Buckets.Length == 0 ? 3.0 : 0.4);
    }

    private void PrepareWaves(AmsComponent ams, Sequence seq)
    {
        if (!TryComp<MapGridComponent>(seq.Grid, out var grid) || ams.WaveCount <= 0)
            return;

        var origin = _map.TileIndicesFor(seq.Grid, grid, seq.EpicenterLocal);
        var tiles = new List<(Vector2i Pos, float Dist)>();
        var max = 1f;

        foreach (var tile in _map.GetAllTiles(seq.Grid, grid))
        {
            var dx = tile.GridIndices.X - origin.X;
            var dy = tile.GridIndices.Y - origin.Y;
            var dist = MathF.Sqrt(dx * dx + dy * dy);
            tiles.Add((tile.GridIndices, dist));
            max = MathF.Max(max, dist);
        }

        seq.MaxDist = max;
        seq.Buckets = new List<Vector2i>[ams.WaveCount];
        for (var i = 0; i < ams.WaveCount; i++)
            seq.Buckets[i] = new List<Vector2i>();

        foreach (var (pos, dist) in tiles)
            seq.Buckets[Math.Min(ams.WaveCount - 1, (int) (dist / max * ams.WaveCount))].Add(pos);

        var lamps = EntityQueryEnumerator<PoweredLightComponent, TransformComponent>();
        while (lamps.MoveNext(out var lamp, out _, out var xform))
        {
            if (xform.GridUid != seq.Grid)
                continue;

            var tp = _map.TileIndicesFor(seq.Grid, grid, xform.Coordinates);
            var dx = tp.X - origin.X;
            var dy = tp.Y - origin.Y;
            seq.Lights.Add((lamp, MathF.Sqrt(dx * dx + dy * dy)));
        }
    }

    private void DoWave(EntityUid uid, AmsComponent ams, Sequence seq, TimeSpan now)
    {
        if (seq.Wave >= seq.Buckets.Length || !TryComp<MapGridComponent>(seq.Grid, out var grid))
        {
            Finish(uid, ams, seq);
            return;
        }

        var bucket = seq.Buckets[seq.Wave];
        var count = Math.Min(bucket.Count, ams.ExplosionsPerWave);

        for (var i = 0; i < count; i++)
        {
            var tile = bucket[_random.Next(bucket.Count)];
            var coords = _map.GridTileToLocal(seq.Grid, grid, tile);

            _explosion.QueueExplosion(_transform.ToMapCoordinates(coords), ams.ExplosionType, ams.WaveIntensity,
                ams.WaveSlope, ams.WaveMaxTileIntensity, null);

            Spawn("AmsEffectBlastFlash", coords);

            if (i == 0)
                _audio.PlayPvs(CascadeSound, coords, AudioParams.Default.WithMaxDistance(45f));
        }

        if (seq.Wave % 3 == 0)
            Spawn(seq.Wave < seq.Buckets.Length / 2 ? "AmsEffectShockwaveLarge" : "AmsEffectShockwaveHuge", seq.EpicenterLocal);

        var radius = seq.MaxDist * (seq.Wave + 1) / seq.Buckets.Length;
        seq.Lights.RemoveAll(l =>
        {
            if (l.Dist > radius)
                return false;

            if (Exists(l.Light))
                _poweredLight.TryDestroyBulb(l.Light);
            return true;
        });

        Kick(seq, 1.4f);

        seq.Wave++;

        seq.NextWave = now + TimeSpan.FromSeconds(seq.Wave >= seq.Buckets.Length ? 2.5 : ams.WaveInterval);
    }

    private void Finish(EntityUid uid, AmsComponent ams, Sequence seq)
    {
        if (seq.HasGrid)
            KillRemainingOnGrid(seq.Grid);

        Kick(seq, 2.5f);
        Announce(Loc.GetString("ams-announce-detonated"), AnnounceSound, Color.Red);

        _roundEnd.EndRound(TimeSpan.FromSeconds(ams.RoundEndDelay));

        _sequences.Remove(uid);
        QueueDel(uid);
    }

    private void KillRemainingOnGrid(EntityUid grid)
    {
        var query = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var mob, out var state, out var xform))
        {
            if (xform.GridUid != grid || state.CurrentState == MobState.Dead || _amsRoundEnd.IsEvacuated(mob))
                continue;

            _mobState.ChangeMobState(mob, MobState.Dead, state);
        }
    }

    private void Kick(Sequence seq, float strength)
    {
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } ent || Transform(ent).MapID != seq.Map)
                continue;

            var dist = (_transform.GetWorldPosition(ent) - seq.Epicenter.Position).Length();
            _recoil.KickCamera(ent, _random.NextAngle().ToVec() * (strength / (1f + dist / 12f)));
        }
    }

    #endregion
}
