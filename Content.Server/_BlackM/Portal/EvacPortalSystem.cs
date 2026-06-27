using Content.Server.Chat.Systems;
using Content.Server.Nuke;
using Content.Server.RoundEnd;
using Content.Shared._BlackM.Portal;
using Content.Shared.Nuke;
using Content.Server.Light.Components;
using Content.Server.Light.EntitySystems;
using Content.Shared.Light.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.Portal;

public sealed class EvacPortalSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly NukeSystem _nuke = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly PoweredLightSystem _poweredLight = default!;
    [Dependency] private readonly PointLightSystem _pointLight = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    private static readonly SoundPathSpecifier SyncSound =
        new("/Audio/_BlackM/Announcements/announcesync.ogg");
    private static readonly SoundPathSpecifier LaunchSound =
        new("/Audio/_BlackM/Announcements/announce.ogg");
    private static readonly SoundPathSpecifier WarningSound =
        new("/Audio/_BlackM/Announcements/announce.ogg");
    private static readonly SoundPathSpecifier HecuSound =
        new("/Audio/_BlackM/Announcements/hecucrack.ogg");

    // Saved states for blackout/restore.
    private readonly Dictionary<EntityUid, bool> _savedLightState = new();
    private readonly Dictionary<EntityUid, Color> _savedEmergencyColors = new();

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<EvacPortalComponent>(EvacPortalUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<EvacPortalLaunchMessage>(OnLaunchPressed);
            subs.Event<EvacPortalTeleportMessage>(OnTeleportPressed);
        });

        SubscribeLocalEvent<NukeExplodedEvent>(OnNukeExploded);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<EvacPortalComponent>();
        while (query.MoveNext(out var uid, out var portal))
            UpdatePortal(uid, portal);
    }

    private void UpdatePortal(EntityUid uid, EvacPortalComponent portal)
    {
        var now = _timing.CurTime;

        switch (portal.Status)
        {
            case EvacPortalStatus.Synchronizing:
                if (portal.SyncStartTime.HasValue)
                {
                    var elapsed = (float)(now - portal.SyncStartTime.Value).TotalSeconds;
                    portal.EnergyCharge = Math.Clamp(elapsed / portal.ChargeTime, 0f, 1f);

                    if (!portal.HecuSpawned && elapsed >= portal.HecuSpawnDelay)
                    {
                        portal.HecuSpawned = true;
                        Dirty(uid, portal);
                        SpawnHecuSoldiers();
                        SendHecuAnnouncement();
                    }

                    if (portal.EnergyCharge >= 1f)
                    {
                        portal.Status = EvacPortalStatus.Ready;
                        portal.EnergyCharge = 1f;
                        Dirty(uid, portal);
                        UpdateUi(uid, portal);
                        SendAnnouncement(Loc.GetString("evac-portal-announce-sync-ready"), SyncSound);

                        RestoreLights();
                        return;
                    }
                }
                break;

            case EvacPortalStatus.Active:
                if (portal.ActiveEndTime.HasValue)
                {
                    var remaining = portal.ActiveEndTime.Value - now;

                    if (!portal.ClosingWarningSent && remaining <= TimeSpan.FromSeconds(15))
                    {
                        portal.ClosingWarningSent = true;
                        Dirty(uid, portal);
                        SendAnnouncement(Loc.GetString("evac-portal-announce-closing-soon"), WarningSound);
                    }

                    if (remaining <= TimeSpan.Zero)
                    {
                        portal.Status = EvacPortalStatus.Used;
                        portal.HasBeenUsed = true;
                        portal.EnergyCharge = 0f;
                        portal.ActiveEndTime = null;
                        portal.ClosingWarningSent = false;
                        Dirty(uid, portal);
                        UpdateUi(uid, portal);
                        ArmNuke();
                        return;
                    }
                }
                break;
        }

        if (portal.Status is EvacPortalStatus.Active or EvacPortalStatus.Synchronizing)
            UpdateUi(uid, portal);
    }

    private void OnUiOpened(Entity<EvacPortalComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent.Owner, ent.Comp);
    }

    private void OnLaunchPressed(Entity<EvacPortalComponent> ent, ref EvacPortalLaunchMessage args)
    {
        var portal = ent.Comp;

        if (portal.HasBeenUsed)
            return;

        switch (portal.Status)
        {
            case EvacPortalStatus.Inactive:
                portal.Status = EvacPortalStatus.Synchronizing;
                portal.SyncStartTime = _timing.CurTime;
                portal.EnergyCharge = 0f;
                portal.HecuSpawned = false;
                SendAnnouncement(Loc.GetString("evac-portal-announce-sync-started"), SyncSound);

                TurnOffAllLights(ent.Owner);
                break;

            case EvacPortalStatus.Ready:
                portal.Status = EvacPortalStatus.Active;
                portal.ActiveEndTime = _timing.CurTime + TimeSpan.FromSeconds(portal.ActiveDuration);
                portal.ClosingWarningSent = false;
                SendAnnouncement(Loc.GetString("evac-portal-announce-launched"), LaunchSound);
                break;

            default:
                return;
        }

        Dirty(ent.Owner, portal);
        UpdateUi(ent.Owner, portal);
    }

    private void OnTeleportPressed(Entity<EvacPortalComponent> ent, ref EvacPortalTeleportMessage args)
    {
        var portal = ent.Comp;

        if (portal.Status != EvacPortalStatus.Active)
            return;

        var player = args.Actor;
        if (!Exists(player))
            return;

        var destination = FindDestination();
        if (destination == null)
            return;

        var destCoords = Transform(destination.Value).Coordinates;
        _transform.SetCoordinates(player, destCoords);
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
        // Restore powered lamps to their previous On/Off state.
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

        // Restore emergency light colors.
        var emergencyQuery = EntityQueryEnumerator<EmergencyLightComponent, PointLightComponent>();
        while (emergencyQuery.MoveNext(out var eUid, out _, out var eLight))
        {
            if (_savedEmergencyColors.TryGetValue(eUid, out var savedColor))
                _pointLight.SetColor(eUid, savedColor, eLight);
        }

        _savedLightState.Clear();
        _savedEmergencyColors.Clear();
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
            Loc.GetString("evac-portal-announce-hecu-intercept"),
            sender: Loc.GetString("evac-portal-announce-hecu-sender"),
            playSound: true,
            announcementSound: HecuSound,
            colorOverride: Color.DarkGreen);
    }

    private EntityUid? FindDestination()
    {
        var query = EntityQueryEnumerator<EvacPortalDestinationComponent>();
        while (query.MoveNext(out var uid, out _))
            return uid;
        return null;
    }

    private void UpdateUi(EntityUid uid, EvacPortalComponent portal)
    {
        _appearance.SetData(uid, EvacPortalVisuals.Active, portal.Status);

        TimeSpan? countdown = portal.Status switch
        {
            EvacPortalStatus.Synchronizing => portal.SyncStartTime.HasValue
                ? portal.SyncStartTime.Value + TimeSpan.FromSeconds(portal.ChargeTime)
                : null,
            EvacPortalStatus.Active => portal.ActiveEndTime,
            _ => null,
        };

        _ui.SetUiState(uid, EvacPortalUiKey.Key,
            new EvacPortalBuiState(portal.Status, portal.EnergyCharge, countdown));
    }

    private void SendAnnouncement(string message, SoundPathSpecifier sound)
    {
        _chat.DispatchGlobalAnnouncement(
            message,
            sender: Loc.GetString("evac-portal-announce-sender"),
            playSound: true,
            announcementSound: sound,
            colorOverride: Color.Cyan);
    }
}
