using Content.Shared._BlackM.Cargo.Delivery;
using Content.Server.Administration.Logs;
using Content.Server.Cargo.Systems;
using Content.Server.Pinpointer;
using Content.Server.Popups;
using Content.Server.Radio.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared.Access.Systems;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Lock;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Content.Shared.Roles;
using Content.Shared.Stacks;
using Content.Shared.Storage.Components;
using Content.Shared.Whitelist;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.Cargo.Delivery;

public sealed partial class CargoDeliverySystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly CargoSystem _cargo = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly LockSystem _lock = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private const float UiUpdateInterval = 1f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CargoDeliveryTerminalComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<CargoDeliveryTerminalComponent, CargoDeliverySendMessage>(OnSend);
        SubscribeLocalEvent<CargoDeliveryTerminalComponent, CargoDeliveryPrintCrateMessage>(OnPrintCrate);
        SubscribeLocalEvent<CargoDeliveryTerminalComponent, CargoDeliveryRecallMessage>(OnRecall);

        SubscribeLocalEvent<CargoDeliveryPointComponent, MapInitEvent>(OnPointMapInit);

        InitializeDrone();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateDrones(frameTime);

        var query = EntityQueryEnumerator<CargoDeliveryTerminalComponent>();
        while (query.MoveNext(out var uid, out var terminal))
        {
            terminal.UiAccumulator += frameTime;
            if (terminal.UiAccumulator < UiUpdateInterval)
                continue;

            terminal.UiAccumulator = 0f;
            UpdateUi(uid, terminal);
        }
    }

    private void OnPointMapInit(EntityUid uid, CargoDeliveryPointComponent comp, MapInitEvent args)
    {
        comp.Incoming = 0;
        _appearance.SetData(uid, CargoDeliveryPointVisuals.Incoming, false);
    }

    #region UI

    private void OnUiOpened(EntityUid uid, CargoDeliveryTerminalComponent comp, BoundUIOpenedEvent args)
    {
        UpdateUi(uid, comp);
    }

    private void UpdateUi(EntityUid uid, CargoDeliveryTerminalComponent comp)
    {
        if (!_ui.IsUiOpen(uid, CargoDeliveryTerminalUiKey.Key))
            return;

        CleanupDrones(comp);

        var now = _timing.CurTime;
        var station = _station.GetOwningStation(uid);
        var padStatus = GetPadStatus(uid, comp, out var pad, out var crate);
        var origin = pad ?? uid;
        var originCoords = _transform.GetMapCoordinates(origin);

        var points = new List<CargoDeliveryPointEntry>();
        foreach (var (point, pointComp) in GetPoints(uid))
        {
            var coords = _transform.GetMapCoordinates(point);
            var distance = (int) (coords.Position - originCoords.Position).Length();
            var cooldown = comp.PointCooldowns.TryGetValue(point, out var until) && until > now
                ? (int) Math.Ceiling((until - now).TotalSeconds)
                : 0;

            var available = Transform(point).Anchored && distance >= comp.MinPointDistance;
            var color = Color.White;
            var deptName = pointComp.Department.Id;
            if (_proto.TryIndex(pointComp.Department, out var dept))
            {
                color = dept.Color;
                deptName = Loc.GetString(dept.Name);
            }

            points.Add(new CargoDeliveryPointEntry(GetNetEntity(point),
                MetaData(point).EntityPrototype?.ID ?? string.Empty,
                MetaData(point).EntityName, deptName, color,
                _navMap.GetNearestBeaconString(coords), distance, available, cooldown, pointComp.Incoming));
        }

        points.Sort((a, b) => string.Compare(a.DepartmentName, b.DepartmentName, StringComparison.CurrentCultureIgnoreCase));

        var contents = new List<CargoDeliveryContentEntry>();
        string? crateProto = null;
        if (crate != null && TryComp<EntityStorageComponent>(crate.Value, out var storage))
        {
            crateProto = MetaData(crate.Value).EntityPrototype?.ID;
            var grouped = new Dictionary<string, CargoDeliveryContentEntry>();
            foreach (var item in storage.Contents.ContainedEntities)
            {
                var meta = MetaData(item);
                var protoId = meta.EntityPrototype?.ID ?? string.Empty;
                var key = protoId + "|" + meta.EntityName;
                var count = TryComp<StackComponent>(item, out var stack) ? stack.Count : 1;

                if (grouped.TryGetValue(key, out var existing))
                    existing.Count += count;
                else
                    grouped[key] = new CargoDeliveryContentEntry(protoId, meta.EntityName, count);
            }

            contents.AddRange(grouped.Values);
            contents.Sort((a, b) => b.Count.CompareTo(a.Count));
        }

        var drones = new List<CargoDeliveryDroneEntry>();
        var idle = 0;
        foreach (var (drone, droneComp) in GetFleet(origin, comp))
        {
            if (droneComp.State == CargoDeliveryDroneState.Idle)
                idle++;

            var integrity = 100;
            if (TryComp<DamageableComponent>(drone, out var damageable) && droneComp.DownThreshold > 0)
            {
                var frac = 1f - damageable.TotalDamage.Float() / droneComp.DownThreshold;
                integrity = (int) Math.Clamp(frac * 100f, 0f, 100f);
            }

            drones.Add(new CargoDeliveryDroneEntry(GetNetEntity(drone),
                MetaData(drone).EntityPrototype?.ID ?? string.Empty,
                MetaData(drone).EntityName, droneComp.State, droneComp.Crate != null,
                comp.ActiveDrones.Contains(drone), droneComp.DestinationName,
                GetDistanceToTarget(drone, droneComp),
                droneComp.State == CargoDeliveryDroneState.Downed ? (int) Math.Ceiling(droneComp.StateTimer) : 0,
                integrity));
        }

        drones.Sort((a, b) => (b.Own ? 1 : 0).CompareTo(a.Own ? 1 : 0));

        var balance = 0;
        if (station != null && TryComp<StationBankAccountComponent>(station.Value, out var bank))
            balance = _cargo.GetBalanceFromAccount((station.Value, bank), comp.Account);

        var printCooldown = comp.NextCratePrint > now ? (int) Math.Ceiling((comp.NextCratePrint - now).TotalSeconds) : 0;

        _ui.SetUiState(uid, CargoDeliveryTerminalUiKey.Key, new CargoDeliveryTerminalState(
            points, padStatus, crateProto, contents, drones, idle, balance,
            comp.Reward, comp.Penalty, comp.CratePrice, printCooldown, comp.TotalDelivered, comp.TotalFailed));
    }

    private int GetDistanceToTarget(EntityUid drone, CargoDeliveryDroneComponent comp)
    {
        var pos = _transform.GetMapCoordinates(drone);
        MapCoordinates target;

        switch (comp.State)
        {
            case CargoDeliveryDroneState.Delivering:
                if (comp.Point is not { } point || TerminatingOrDeleted(point))
                    return -1;
                target = _transform.GetMapCoordinates(point);
                break;
            case CargoDeliveryDroneState.ToPad:
            case CargoDeliveryDroneState.Loading:
                if (comp.Pad is not { } pad || TerminatingOrDeleted(pad))
                    return -1;
                target = _transform.GetMapCoordinates(pad);
                break;
            default:
                target = GetHome(drone, comp);
                break;
        }

        if (target.MapId != pos.MapId)
            return -1;

        return (int) (target.Position - pos.Position).Length();
    }

    #endregion

    #region Terminal actions

    private void OnSend(EntityUid uid, CargoDeliveryTerminalComponent comp, CargoDeliverySendMessage args)
    {
        var user = args.Actor;

        if (!_access.IsAllowed(user, uid))
        {
            Deny(uid, comp, user, "cargo-delivery-error-access");
            return;
        }

        var point = GetEntity(args.Point);
        if (!TryComp<CargoDeliveryPointComponent>(point, out var pointComp) || TerminatingOrDeleted(point))
        {
            Deny(uid, comp, user, "cargo-delivery-error-no-point");
            return;
        }

        if (!IsPointValidFor(uid, point))
        {
            Deny(uid, comp, user, "cargo-delivery-error-point-unavailable");
            return;
        }

        var now = _timing.CurTime;
        if (comp.PointCooldowns.TryGetValue(point, out var until) && until > now)
        {
            Deny(uid, comp, user, "cargo-delivery-error-cooldown");
            return;
        }

        var status = GetPadStatus(uid, comp, out var pad, out var crate);
        if (status != CargoDeliveryPadStatus.Ready || pad == null || crate == null)
        {
            Deny(uid, comp, user, GetPadStatusLoc(status));
            return;
        }

        var padCoords = _transform.GetMapCoordinates(pad.Value);
        var pointCoords = _transform.GetMapCoordinates(point);
        if (padCoords.MapId != pointCoords.MapId)
        {
            Deny(uid, comp, user, "cargo-delivery-error-point-unavailable");
            return;
        }

        if ((padCoords.Position - pointCoords.Position).Length() < comp.MinPointDistance)
        {
            Deny(uid, comp, user, "cargo-delivery-error-too-close");
            return;
        }

        var drone = FindFreeDrone(pad.Value, comp);
        if (drone == null)
        {
            Deny(uid, comp, user, "cargo-delivery-error-no-drones");
            return;
        }

        Dispatch(uid, comp, user, pad.Value, crate.Value, (point, pointComp), drone.Value);
    }

    private void Dispatch(EntityUid terminal, CargoDeliveryTerminalComponent comp, EntityUid user, EntityUid pad,
        EntityUid crate, Entity<CargoDeliveryPointComponent> point, Entity<CargoDeliveryDroneComponent> drone)
    {
        var droneComp = drone.Comp;
        var destination = MetaData(point).EntityName;

        droneComp.Terminal = terminal;
        droneComp.Pad = pad;
        droneComp.PendingCrate = crate;
        droneComp.Crate = null;
        droneComp.DestinationName = destination;
        droneComp.WanderTarget = null;
        droneComp.FlightTime = 0f;
        droneComp.Penalized = false;
        droneComp.State = CargoDeliveryDroneState.ToPad;
        SetPointTarget(drone, point);

        _lock.Lock(crate, null);
        var lockComp = EnsureComp<CargoDeliveryLockComponent>(crate);
        lockComp.Access = new(point.Comp.Access);
        lockComp.DestinationName = destination;
        Dirty(crate, lockComp);

        comp.ActiveDrones.Add(drone);
        comp.PointCooldowns[point] = _timing.CurTime + comp.PointCooldown;

        UpdateDroneVisuals(drone, droneComp);
        _audio.PlayPvs(comp.ConfirmSound, terminal);

        SendRadio(terminal, comp.RadioChannel, Loc.GetString("cargo-delivery-radio-dispatched",
            ("drone", MetaData(drone).EntityName), ("point", destination)));

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(user):user} sent cargo delivery drone {ToPrettyString(drone):drone} with {ToPrettyString(crate):crate} to {ToPrettyString(point):target}");

        UpdateUi(terminal, comp);
    }

    private void OnPrintCrate(EntityUid uid, CargoDeliveryTerminalComponent comp, CargoDeliveryPrintCrateMessage args)
    {
        var user = args.Actor;

        if (!_access.IsAllowed(user, uid))
        {
            Deny(uid, comp, user, "cargo-delivery-error-access");
            return;
        }

        if (comp.NextCratePrint > _timing.CurTime)
        {
            Deny(uid, comp, user, "cargo-delivery-error-print-cooldown");
            return;
        }

        var status = GetPadStatus(uid, comp, out var pad, out _);
        if (pad == null)
        {
            Deny(uid, comp, user, "cargo-delivery-pad-status-no-pad");
            return;
        }

        if (status != CargoDeliveryPadStatus.NoCrate)
        {
            Deny(uid, comp, user, "cargo-delivery-error-pad-occupied");
            return;
        }

        if (_station.GetOwningStation(uid) is not { } station ||
            !TryComp<StationBankAccountComponent>(station, out var bank))
        {
            Deny(uid, comp, user, "cargo-delivery-error-no-station");
            return;
        }

        if (_cargo.GetBalanceFromAccount((station, bank), comp.Account) < comp.CratePrice)
        {
            Deny(uid, comp, user, "cargo-delivery-error-no-money");
            return;
        }

        _cargo.UpdateBankAccount((station, bank), -comp.CratePrice, comp.Account);
        Spawn(comp.CrateProto, Transform(pad.Value).Coordinates);
        comp.NextCratePrint = _timing.CurTime + comp.CratePrintCooldown;
        _audio.PlayPvs(comp.ConfirmSound, uid);

        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(user):user} printed a cargo delivery crate");
        UpdateUi(uid, comp);
    }

    private void OnRecall(EntityUid uid, CargoDeliveryTerminalComponent comp, CargoDeliveryRecallMessage args)
    {
        var user = args.Actor;

        if (!_access.IsAllowed(user, uid))
        {
            Deny(uid, comp, user, "cargo-delivery-error-access");
            return;
        }

        var drone = GetEntity(args.Drone);
        if (!comp.ActiveDrones.Contains(drone) || !TryComp<CargoDeliveryDroneComponent>(drone, out var droneComp))
            return;

        if (droneComp.State is not (CargoDeliveryDroneState.ToPad or CargoDeliveryDroneState.Loading or CargoDeliveryDroneState.Delivering))
        {
            Deny(uid, comp, user, "cargo-delivery-error-cant-recall");
            return;
        }

        Abort(drone, droneComp, "cargo-delivery-abort-recalled");
        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(user):user} recalled cargo delivery drone {ToPrettyString(drone):drone}");
        UpdateUi(uid, comp);
    }

    private void Deny(EntityUid terminal, CargoDeliveryTerminalComponent comp, EntityUid user, string loc)
    {
        _popup.PopupEntity(Loc.GetString(loc), terminal, user, PopupType.SmallCaution);
        _audio.PlayPvs(comp.ErrorSound, terminal);
        UpdateUi(terminal, comp);
    }

    #endregion

    #region Helpers

    private void CleanupDrones(CargoDeliveryTerminalComponent comp)
    {
        comp.ActiveDrones.RemoveWhere(d => TerminatingOrDeleted(d) ||
            !TryComp<CargoDeliveryDroneComponent>(d, out var drone) ||
            drone.State == CargoDeliveryDroneState.Idle);
    }

    private bool IsPointValidFor(EntityUid terminal, EntityUid point)
    {
        var pointXform = Transform(point);
        if (!pointXform.Anchored || pointXform.MapID != Transform(terminal).MapID)
            return false;

        var terminalStation = _station.GetOwningStation(terminal);
        return terminalStation == null || _station.GetOwningStation(point) == terminalStation;
    }

    private List<(EntityUid, CargoDeliveryPointComponent)> GetPoints(EntityUid terminal)
    {
        var result = new List<(EntityUid, CargoDeliveryPointComponent)>();
        var mapId = Transform(terminal).MapID;
        var station = _station.GetOwningStation(terminal);

        var query = EntityQueryEnumerator<CargoDeliveryPointComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            if (station != null && _station.GetOwningStation(uid, xform) != station)
                continue;

            result.Add((uid, comp));
        }

        return result;
    }

    private List<(EntityUid, CargoDeliveryDroneComponent)> GetFleet(EntityUid origin, CargoDeliveryTerminalComponent comp)
    {
        var result = new List<(EntityUid, CargoDeliveryDroneComponent)>();
        var originCoords = _transform.GetMapCoordinates(origin);

        var query = EntityQueryEnumerator<CargoDeliveryDroneComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var drone, out var xform))
        {
            if (comp.ActiveDrones.Contains(uid))
            {
                result.Add((uid, drone));
                continue;
            }

            var home = GetHome(uid, drone);
            if (home.MapId != originCoords.MapId)
                continue;

            if ((home.Position - originCoords.Position).Length() > comp.DroneSearchRange)
                continue;

            result.Add((uid, drone));
        }

        return result;
    }

    private Entity<CargoDeliveryDroneComponent>? FindFreeDrone(EntityUid pad, CargoDeliveryTerminalComponent comp)
    {
        var padCoords = _transform.GetMapCoordinates(pad);
        Entity<CargoDeliveryDroneComponent>? best = null;
        var bestDist = float.MaxValue;

        foreach (var (uid, drone) in GetFleet(pad, comp))
        {
            if (drone.State != CargoDeliveryDroneState.Idle)
                continue;

            var pos = _transform.GetMapCoordinates(uid);
            if (pos.MapId != padCoords.MapId)
                continue;

            var dist = (pos.Position - padCoords.Position).LengthSquared();
            if (dist >= bestDist)
                continue;

            best = (uid, drone);
            bestDist = dist;
        }

        return best;
    }

    private EntityUid? FindPad(EntityUid terminal, CargoDeliveryTerminalComponent comp)
    {
        var xform = Transform(terminal);
        var terminalPos = _transform.GetWorldPosition(xform);

        EntityUid? best = null;
        var bestDist = float.MaxValue;

        foreach (var pad in _lookup.GetEntitiesInRange<CargoDeliveryPadComponent>(xform.Coordinates, comp.PadSearchRange))
        {
            var padXform = Transform(pad.Owner);
            if (padXform.GridUid != xform.GridUid)
                continue;

            var dist = (_transform.GetWorldPosition(padXform) - terminalPos).LengthSquared();
            if (dist >= bestDist)
                continue;

            best = pad.Owner;
            bestDist = dist;
        }

        return best;
    }

    private EntityUid? FindCrateOnPad(EntityUid pad, CargoDeliveryTerminalComponent? comp)
    {
        var padComp = Comp<CargoDeliveryPadComponent>(pad);
        foreach (var storage in _lookup.GetEntitiesInRange<EntityStorageComponent>(Transform(pad).Coordinates, padComp.CrateSearchRange))
        {
            if (Transform(storage.Owner).Anchored)
                continue;

            if (comp?.CrateWhitelist != null && !_whitelist.IsValid(comp.CrateWhitelist, storage.Owner))
                continue;

            return storage.Owner;
        }

        return null;
    }

    private bool IsCrateOnPad(EntityUid pad, EntityUid crate)
    {
        if (TerminatingOrDeleted(crate) || _container.IsEntityInContainer(crate))
            return false;

        var padComp = Comp<CargoDeliveryPadComponent>(pad);
        var padCoords = _transform.GetMapCoordinates(pad);
        var crateCoords = _transform.GetMapCoordinates(crate);
        return padCoords.MapId == crateCoords.MapId &&
               (padCoords.Position - crateCoords.Position).Length() <= padComp.CrateSearchRange + 0.25f;
    }

    private CargoDeliveryPadStatus GetPadStatus(EntityUid terminal, CargoDeliveryTerminalComponent comp,
        out EntityUid? pad, out EntityUid? crate)
    {
        crate = null;
        pad = FindPad(terminal, comp);
        if (pad == null)
            return CargoDeliveryPadStatus.NoPad;

        crate = FindCrateOnPad(pad.Value, comp);
        if (crate == null)
            return CargoDeliveryPadStatus.NoCrate;

        if (HasComp<CargoDeliveryLockComponent>(crate.Value))
            return CargoDeliveryPadStatus.CrateReserved;

        var storageComp = Comp<EntityStorageComponent>(crate.Value);
        if (storageComp.Open)
            return CargoDeliveryPadStatus.CrateOpen;

        if (storageComp.Contents.ContainedEntities.Count == 0)
            return CargoDeliveryPadStatus.CrateEmpty;

        return CheckContents(crate.Value, comp.ContentsBlacklist, 0);
    }

    private CargoDeliveryPadStatus CheckContents(EntityUid uid, EntityWhitelist? blacklist, int depth)
    {
        if (depth > 8 || !TryComp<ContainerManagerComponent>(uid, out var manager))
            return CargoDeliveryPadStatus.Ready;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var contained in container.ContainedEntities)
            {
                if (HasComp<MobStateComponent>(contained))
                    return CargoDeliveryPadStatus.CrateHasMob;

                if (blacklist != null && _whitelist.IsValid(blacklist, contained))
                    return CargoDeliveryPadStatus.CrateHasForbidden;

                var nested = CheckContents(contained, blacklist, depth + 1);
                if (nested != CargoDeliveryPadStatus.Ready)
                    return nested;
            }
        }

        return CargoDeliveryPadStatus.Ready;
    }

    private static string GetPadStatusLoc(CargoDeliveryPadStatus status)
    {
        return status switch
        {
            CargoDeliveryPadStatus.NoPad => "cargo-delivery-pad-status-no-pad",
            CargoDeliveryPadStatus.NoCrate => "cargo-delivery-pad-status-no-crate",
            CargoDeliveryPadStatus.CrateOpen => "cargo-delivery-pad-status-open",
            CargoDeliveryPadStatus.CrateEmpty => "cargo-delivery-pad-status-empty",
            CargoDeliveryPadStatus.CrateHasMob => "cargo-delivery-pad-status-mob",
            CargoDeliveryPadStatus.CrateHasForbidden => "cargo-delivery-pad-status-forbidden",
            CargoDeliveryPadStatus.CrateReserved => "cargo-delivery-pad-status-reserved",
            _ => "cargo-delivery-pad-status-ready",
        };
    }

    private void ChangeBalance(EntityUid source, ProtoId<CargoAccountPrototype> account, int amount)
    {
        if (_station.GetOwningStation(source) is not { } station ||
            !TryComp<StationBankAccountComponent>(station, out var bank))
            return;

        _cargo.UpdateBankAccount((station, bank), amount, account);
    }

    private void SendRadio(EntityUid source, ProtoId<RadioChannelPrototype> channel, string message)
    {
        _radio.SendRadioMessage(source, message, channel, source);
    }

    #endregion
}
