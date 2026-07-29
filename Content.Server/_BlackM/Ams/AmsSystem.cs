using Content.Server._BlackM.OneWayTeleport;
using Content.Server.Chat.Systems;
using Content.Server.Nuke;
using Content.Server.RoundEnd;
using Content.Shared._BlackM.Ams;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Nuke;
using Content.Server.Light.Components;
using Content.Server.Light.EntitySystems;
using Content.Shared.Light.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.Ams;

public sealed class AmsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly NukeSystem _nuke = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly PoweredLightSystem _poweredLight = default!;
    [Dependency] private readonly PointLightSystem _pointLight = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly OneWayTeleportSystem _oneWayTeleport = default!;

    private const string EvacuationDestinationId = "ams_evacuation";

    private static readonly SoundPathSpecifier SyncSound =
        new("/Audio/_BlackM/Announcements/announcesync.ogg");
    private static readonly SoundPathSpecifier LaunchSound =
        new("/Audio/_BlackM/Announcements/announce.ogg");
    private static readonly SoundPathSpecifier WarningSound =
        new("/Audio/_BlackM/Announcements/announce.ogg");
    private static readonly SoundPathSpecifier HecuSound =
        new("/Audio/_BlackM/Announcements/hecucrack.ogg");
    private static readonly SoundPathSpecifier EvacSound =
        new("/Audio/_BlackM/Announcements/announce.ogg");

    private readonly Dictionary<EntityUid, bool> _savedLightState = new();
    private readonly Dictionary<EntityUid, Color> _savedEmergencyColors = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NukeExplodedEvent>(OnNukeExploded);

        SubscribeLocalEvent<AmsComponent, EntInsertedIntoContainerMessage>(OnPartInserted);
        SubscribeLocalEvent<AmsComponent, EntRemovedFromContainerMessage>(OnPartRemoved);
        SubscribeLocalEvent<AmsComponent, AmsLaunchButtonMessage>(OnLaunchButton);
    }

    private void OnPartInserted(EntityUid uid, AmsComponent ams, EntInsertedIntoContainerMessage args)
    {
        if (!ams.PartSlots.Contains(args.Container.ID))
            return;

        var slotId = args.Container.ID;

        ams.FilledSlots.Add(slotId);
        ams.CalibratedSlots.Remove(slotId);

        ams.CalibrationStartTimes[slotId] = _timing.CurTime;

        Dirty(uid, ams);
    }

    private void OnPartRemoved(EntityUid uid, AmsComponent ams, EntRemovedFromContainerMessage args)
    {
        if (!ams.PartSlots.Contains(args.Container.ID))
            return;

        var slotId = args.Container.ID;

        ams.FilledSlots.Remove(slotId);
        ams.CalibratedSlots.Remove(slotId);
        ams.CalibrationStartTimes.Remove(slotId);

        Dirty(uid, ams);
    }

    private void OnLaunchButton(EntityUid uid, AmsComponent ams, AmsLaunchButtonMessage args)
    {
        TryLaunch(uid, ams);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<AmsComponent>();
        while (query.MoveNext(out var uid, out var portal))
        {
            UpdateCalibration(uid, portal);
            UpdatePortal(uid, portal);
        }
    }

    private void UpdateCalibration(EntityUid uid, AmsComponent ams)
    {
        if (ams.CalibrationStartTimes.Count == 0)
            return;

        var now = _timing.CurTime;
        List<string>? finished = null;

        foreach (var (slotId, startTime) in ams.CalibrationStartTimes)
        {
            if ((now - startTime).TotalSeconds < ams.CalibrationDuration)
                continue;

            finished ??= new List<string>();
            finished.Add(slotId);
        }

        if (finished == null)
            return;

        foreach (var slotId in finished)
        {
            ams.CalibrationStartTimes.Remove(slotId);
            ams.CalibratedSlots.Add(slotId);
        }

        Dirty(uid, ams);
    }

    private void UpdatePortal(EntityUid uid, AmsComponent portal)
    {
        var now = _timing.CurTime;

        switch (portal.Status)
        {
            case AmsStatus.Synchronizing:
                if (portal.SyncStartTime.HasValue)
                {
                    var elapsed = (float)(now - portal.SyncStartTime.Value).TotalSeconds;
                    portal.EnergyCharge = Math.Clamp(elapsed / portal.ChargeTime, 0f, 1f);
                    Dirty(uid, portal);

                    if (!portal.HecuSpawned && elapsed >= portal.HecuSpawnDelay)
                    {
                        portal.HecuSpawned = true;
                        Dirty(uid, portal);
                        SpawnHecuSoldiers();
                        SendHecuAnnouncement();
                    }

                    if (portal.EnergyCharge >= 1f)
                    {
                        portal.Status = AmsStatus.Ready;
                        portal.EnergyCharge = 1f;
                        Dirty(uid, portal);
                        SendAnnouncement(Loc.GetString("ams-announce-sync-ready"), SyncSound);

                        RestoreLights();
                        return;
                    }
                }
                break;

            case AmsStatus.Active:
                if (portal.ActiveEndTime.HasValue)
                {
                    var remaining = portal.ActiveEndTime.Value - now;

                    if (!portal.ClosingWarningSent && remaining <= TimeSpan.FromSeconds(15))
                    {
                        portal.ClosingWarningSent = true;
                        Dirty(uid, portal);
                        SendAnnouncement(Loc.GetString("ams-announce-closing-soon"), WarningSound);
                    }

                    if (remaining <= TimeSpan.Zero)
                    {
                        portal.Status = AmsStatus.Used;
                        portal.HasBeenUsed = true;
                        portal.EnergyCharge = 0f;
                        portal.ActiveEndTime = null;
                        portal.ClosingWarningSent = false;
                        Dirty(uid, portal);
                        ArmNuke();
                        return;
                    }
                }
                break;
        }
    }

    public void TryLaunch(EntityUid uid, AmsComponent portal)
    {
        if (portal.HasBeenUsed)
            return;

        switch (portal.Status)
        {
            case AmsStatus.Inactive:
                if (!portal.AllPartsInserted)
                    return;

                portal.Status = AmsStatus.Synchronizing;
                portal.SyncStartTime = _timing.CurTime;
                portal.EnergyCharge = 0f;
                portal.HecuSpawned = false;
                SendAnnouncement(Loc.GetString("ams-announce-sync-started"), SyncSound);

                TurnOffAllLights(uid);
                LockSlots(uid, true);
                break;

            case AmsStatus.Ready:
                portal.Status = AmsStatus.Active;
                portal.ActiveEndTime = _timing.CurTime + TimeSpan.FromSeconds(portal.ActiveDuration);
                portal.ClosingWarningSent = false;
                SendAnnouncement(Loc.GetString("ams-announce-launched"), LaunchSound);

                OpenEvacuationPoints();
                break;

            default:
                return;
        }

        Dirty(uid, portal);
    }

    private void LockSlots(EntityUid uid, bool locked)
    {
        if (!TryComp<AmsComponent>(uid, out var ams))
            return;

        foreach (var slotId in ams.PartSlots)
        {
            if (_itemSlots.TryGetSlot(uid, slotId, out var slot))
                _itemSlots.SetLock(uid, slot, locked);
        }
    }

    private void TurnOffAllLights(EntityUid portalUid)
    {
        _savedLightState.Clear();
        _savedEmergencyColors.Clear();

        var portalGrid = Transform(portalUid).GridUid;
        if (portalGrid == null)
            return;

        var lampQuery = EntityQueryEnumerator<PoweredLightComponent>();
        while (lampQuery.MoveNext(out var uid, out var lamp))
        {
            if (Transform(uid).GridUid != portalGrid)
                continue;

            if (HasComp<EmergencyLightComponent>(uid))
                continue;

            _savedLightState[uid] = lamp.On;

            if (lamp.On)
                _poweredLight.SetState(uid, false, lamp);
        }

        var emergencyQuery = EntityQueryEnumerator<EmergencyLightComponent, PointLightComponent>();
        while (emergencyQuery.MoveNext(out var eUid, out _, out var eLight))
        {
            if (Transform(eUid).GridUid != portalGrid)
                continue;

            _savedEmergencyColors[eUid] = eLight.Color;
            _pointLight.SetEnabled(eUid, true, eLight);
            _pointLight.SetColor(eUid, Color.Red, eLight);
        }
    }

    private void RestoreLights()
    {
        var lampQuery = EntityQueryEnumerator<PoweredLightComponent>();
        while (lampQuery.MoveNext(out var uid, out var lamp))
        {
            if (!_savedLightState.TryGetValue(uid, out var wasOn))
                continue;

            if (wasOn && !lamp.On)
                _poweredLight.SetState(uid, true, lamp);
            else if (!wasOn && lamp.On)
                _poweredLight.SetState(uid, false, lamp);
        }

        var emergencyQuery = EntityQueryEnumerator<EmergencyLightComponent, PointLightComponent>();
        while (emergencyQuery.MoveNext(out var eUid, out _, out var eLight))
        {
            if (_savedEmergencyColors.TryGetValue(eUid, out var savedColor))
                _pointLight.SetColor(eUid, savedColor, eLight);
        }

        _savedLightState.Clear();
        _savedEmergencyColors.Clear();
    }

    private void OpenEvacuationPoints()
    {
        var opened = _oneWayTeleport.SetGroupEnabled(EvacuationDestinationId, true);

        if (opened == 0)
        {
            Log.Warning($"AMS: точки'{EvacuationDestinationId}' не найдены на карте.");
            return;
        }

        _chat.DispatchGlobalAnnouncement(
            Loc.GetString("ams-announce-evac-available"),
            sender: Loc.GetString("ams-announce-sender"),
            playSound: true,
            announcementSound: EvacSound,
            colorOverride: Color.Cyan);
    }

    private void ArmNuke()
    {
        var query = EntityQueryEnumerator<NukeComponent>();
        while (query.MoveNext(out var nukeUid, out var nukeComp))
        {
            if (nukeComp.Status == NukeStatus.ARMED)
                return;

            _nuke.ArmBomb(nukeUid, nukeComp);
            return;
        }
    }

    private void OnNukeExploded(NukeExplodedEvent ev)
    {
        _roundEnd.EndRound(TimeSpan.FromSeconds(30));
    }

    private void SpawnHecuSoldiers()
    {
        var spawnQuery = EntityQueryEnumerator<HecuSpawnPointComponent>();
        while (spawnQuery.MoveNext(out var spawnUid, out _))
        {
            var coords = Transform(spawnUid).Coordinates;
            Spawn("RandomHumanoidHECUSoldierSpawner", coords);
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

    private void SendAnnouncement(string message, SoundPathSpecifier sound)
    {
        _chat.DispatchGlobalAnnouncement(
            message,
            sender: Loc.GetString("ams-announce-sender"),
            playSound: true,
            announcementSound: sound,
            colorOverride: Color.Cyan);
    }
}
