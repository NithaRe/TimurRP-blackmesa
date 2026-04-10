using System.Linq;
using Content.Server._CorvaxGoob.Announcer;
using Content.Server.Chat.Systems;
using Content.Shared._BlackM.Evac;
using Content.Shared.Teleportation.Components;
using Content.Shared.Teleportation.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.Evac;

public sealed class EvacConsoleSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly LinkedEntitySystem _linkedEntity = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<EvacConsoleComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<EvacConsoleComponent, EvacConsoleOpenMessage>(OnOpenMessage);
        SubscribeLocalEvent<EvacConsoleComponent, EvacConsoleCloseMessage>(OnCloseMessage);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<EvacConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.TargetTime == null)
                continue;

            if (_timing.CurTime < comp.TargetTime.Value)
                continue;

            switch (comp.State)
            {
                case EvacConsoleState.Countdown:
                    OpenPortal(uid, comp);
                    break;
                case EvacConsoleState.Open:
                    ClosePortal(uid, comp);
                    break;
            }
        }
    }

    private void OnUIOpened(EntityUid uid, EvacConsoleComponent comp, BoundUIOpenedEvent args)
    {
        UpdateUI(uid, comp);
    }

    private void OnOpenMessage(EntityUid uid, EvacConsoleComponent comp, EvacConsoleOpenMessage args)
    {
        if (comp.State != EvacConsoleState.Idle)
            return;

        if (FindGateway(uid) == null)
            return;

        comp.State = EvacConsoleState.Countdown;
        comp.TargetTime = _timing.CurTime + TimeSpan.FromSeconds(comp.OpenDelay);

        var minutes = (int)(comp.OpenDelay / 60);
        _chat.DispatchStationAnnouncement(uid,
            $"Врата эвакуации активированы. Открытие через {minutes} минут.",
            "Система эвакуации",
            colorOverride: Color.Yellow);

        UpdateUI(uid, comp);
    }

    private void OnCloseMessage(EntityUid uid, EvacConsoleComponent comp, EvacConsoleCloseMessage args)
    {
        if (comp.State == EvacConsoleState.Idle)
            return;

        if (comp.SpawnedPortalDest != null && Exists(comp.SpawnedPortalDest.Value))
            QueueDel(comp.SpawnedPortalDest.Value);

        var gateway = FindGateway(uid);
        if (gateway != null && TryComp<LinkedEntityComponent>(gateway.Value, out var link))
        {
            foreach (var linked in link.LinkedEntities.ToList())
                _linkedEntity.TryUnlink(gateway.Value, linked);
        }

        comp.SpawnedPortalDest = null;
        comp.State = EvacConsoleState.Idle;
        comp.TargetTime = null;

        _chat.DispatchStationAnnouncement(uid,
            "Активация врат эвакуации отменена.",
            "Система эвакуации",
            colorOverride: Color.Red);

        UpdateUI(uid, comp);
    }

    private void OpenPortal(EntityUid uid, EvacConsoleComponent comp)
    {
        var gateway = FindGateway(uid);
        if (gateway == null)
        {
            Log.Error($"EvacConsoleSystem: портал не найден при открытии!");
            comp.State = EvacConsoleState.Idle;
            comp.TargetTime = null;
            UpdateUI(uid, comp);
            return;
        }

        var destMarker = FindDestMarker();
        if (destMarker == null)
        {
            Log.Error($"EvacConsoleSystem: маркер назначения не найден!");
            return;
        }

        var destXform = Transform(destMarker.Value);
        var portalDest = Spawn(comp.PortalDestPrototype, destXform.Coordinates);
        _linkedEntity.TryLink(gateway.Value, portalDest, deleteOnEmptyLinks: false);

        comp.SpawnedPortalDest = portalDest;
        comp.State = EvacConsoleState.Open;
        comp.TargetTime = _timing.CurTime + TimeSpan.FromSeconds(comp.CloseDelay);

        var minutes = (int)(comp.CloseDelay / 60);
        _chat.DispatchStationAnnouncement(uid,
            $"Врата эвакуации открыты. Закроются через {minutes} минут. Проследуйте к вратам эвакуации.",
            "Система эвакуации",
            colorOverride: Color.Green);

        UpdateUI(uid, comp);
    }

    private void ClosePortal(EntityUid uid, EvacConsoleComponent comp)
    {
        if (comp.SpawnedPortalDest != null && Exists(comp.SpawnedPortalDest.Value))
            QueueDel(comp.SpawnedPortalDest.Value);

        var gateway = FindGateway(uid);
        if (gateway != null && TryComp<LinkedEntityComponent>(gateway.Value, out var link))
        {
            foreach (var linked in link.LinkedEntities.ToList())
                _linkedEntity.TryUnlink(gateway.Value, linked);
        }

        comp.SpawnedPortalDest = null;
        comp.State = EvacConsoleState.Idle;
        comp.TargetTime = null;

        _chat.DispatchStationAnnouncement(uid,
            "Врата эвакуации закрыты.",
            "Система эвакуации",
            colorOverride: Color.Red);

        UpdateUI(uid, comp);
    }

    private EntityUid? FindGateway(EntityUid consoleUid)
    {
        var consoleMap = Transform(consoleUid).MapID;
        var query = EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var meta, out var xform))
        {
            if (meta.EntityPrototype?.ID == "PortalMachine" && xform.MapID == consoleMap)
                return uid;
        }
        return null;
    }

    private EntityUid? FindDestMarker()
    {
        var query = EntityQueryEnumerator<EvacPortalMarkerComponent>();
        while (query.MoveNext(out var uid, out var marker))
        {
            if (marker.IsDestination)
                return uid;
        }
        return null;
    }

    private void UpdateUI(EntityUid uid, EvacConsoleComponent comp)
    {
        var portalReady = FindGateway(uid) != null;
        var state = new EvacConsoleBoundUserInterfaceState(comp.State, comp.TargetTime, portalReady);
        _ui.SetUiState(uid, EvacConsoleUiKey.Key, state);
    }
}