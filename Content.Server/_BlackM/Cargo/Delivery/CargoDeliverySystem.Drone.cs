using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._BlackM.Cargo.Delivery;
using Content.Server.Emp;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Utility;

namespace Content.Server._BlackM.Cargo.Delivery;

public sealed partial class CargoDeliverySystem
{
    private const float SafeCheckInterval = 0.25f;

    private void InitializeDrone()
    {
        SubscribeLocalEvent<CargoDeliveryDroneComponent, MapInitEvent>(OnDroneMapInit);
        SubscribeLocalEvent<CargoDeliveryDroneComponent, DamageChangedEvent>(OnDroneDamaged);
        SubscribeLocalEvent<CargoDeliveryDroneComponent, EmpPulseEvent>(OnDroneEmp);
        SubscribeLocalEvent<CargoDeliveryDroneComponent, EntityTerminatingEvent>(OnDroneTerminating);
    }

    private void OnDroneMapInit(EntityUid uid, CargoDeliveryDroneComponent drone, MapInitEvent args)
    {
        drone.Home ??= Transform(uid).Coordinates;
        drone.State = CargoDeliveryDroneState.Idle;
        drone.WanderTimer = _random.NextFloat(0.5f, drone.WanderPauseMax);
        _container.EnsureContainer<ContainerSlot>(uid, drone.ContainerId);
        UpdateDroneVisuals(uid, drone);
    }

    private void UpdateDrones(float frameTime)
    {
        var query = EntityQueryEnumerator<CargoDeliveryDroneComponent, TransformComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var drone, out var xform, out var body))
        {
            switch (drone.State)
            {
                case CargoDeliveryDroneState.Idle:
                    UpdateIdle(uid, drone, xform, body, frameTime);
                    break;

                case CargoDeliveryDroneState.ToPad:
                {
                    drone.FlightTime += frameTime;
                    TrackSafePosition(uid, drone, xform, frameTime);

                    if (drone.Pad is not { } pad || TerminatingOrDeleted(pad) || Transform(pad).MapID != xform.MapID)
                    {
                        Abort(uid, drone, "cargo-delivery-abort-no-pad");
                        break;
                    }

                    if (!ValidatePoint(drone, xform, out var reason))
                    {
                        Abort(uid, drone, reason);
                        break;
                    }

                    if (drone.FlightTime > drone.MaxFlightTime)
                    {
                        Abort(uid, drone, "cargo-delivery-abort-timeout");
                        break;
                    }

                    var target = _transform.GetWorldPosition(pad);
                    if (MoveTowards(uid, drone, xform, body, target, drone.ReturnSpeed) <= drone.ArriveRange)
                    {
                        StopMoving(uid, body);
                        drone.State = CargoDeliveryDroneState.Loading;
                        drone.StateTimer = drone.LoadingTime;
                        UpdateDroneVisuals(uid, drone);
                    }
                    break;
                }

                case CargoDeliveryDroneState.Loading:
                {
                    StopMoving(uid, body);
                    drone.StateTimer -= frameTime;
                    if (drone.StateTimer > 0f)
                        break;

                    if (!ValidatePoint(drone, xform, out var reason))
                    {
                        Abort(uid, drone, reason);
                        break;
                    }

                    if (!TryPickUp(uid, drone))
                    {
                        Abort(uid, drone, "cargo-delivery-abort-crate-missing");
                        break;
                    }

                    drone.State = CargoDeliveryDroneState.Delivering;
                    drone.FlightTime = 0f;
                    UpdateDroneVisuals(uid, drone);
                    break;
                }

                case CargoDeliveryDroneState.Delivering:
                {
                    drone.FlightTime += frameTime;
                    TrackSafePosition(uid, drone, xform, frameTime);

                    if (!ValidatePoint(drone, xform, out var reason))
                    {
                        Abort(uid, drone, reason);
                        break;
                    }

                    if (drone.FlightTime > drone.MaxFlightTime)
                    {
                        Abort(uid, drone, "cargo-delivery-abort-timeout");
                        break;
                    }

                    var target = _transform.GetWorldPosition(drone.Point!.Value);
                    if (MoveTowards(uid, drone, xform, body, target, drone.Speed) <= drone.DeliverRange)
                        Deliver(uid, drone);
                    break;
                }

                case CargoDeliveryDroneState.Downed:
                    StopMoving(uid, body);
                    drone.StateTimer -= frameTime;
                    if (drone.StateTimer <= 0f)
                        Reboot(uid, drone);
                    break;

                case CargoDeliveryDroneState.Returning:
                    UpdateReturning(uid, drone, xform, body, frameTime);
                    break;
            }
        }
    }

    #region Idle / wander

    private void UpdateIdle(EntityUid uid, CargoDeliveryDroneComponent drone, TransformComponent xform,
        PhysicsComponent body, float frameTime)
    {
        if (drone.WanderTarget is { } target)
        {
            if (target.MapId != xform.MapID)
            {
                drone.WanderTarget = null;
                return;
            }

            if (MoveTowards(uid, drone, xform, body, target.Position, drone.WanderSpeed) <= 0.15f)
            {
                StopMoving(uid, body);
                drone.WanderTarget = null;
                drone.WanderTimer = _random.NextFloat(drone.WanderPauseMin, drone.WanderPauseMax);
            }

            return;
        }

        StopMoving(uid, body);
        drone.WanderTimer -= frameTime;
        if (drone.WanderTimer > 0f)
            return;

        drone.WanderTarget = PickWanderTarget(uid, drone);
        drone.WanderTimer = _random.NextFloat(drone.WanderPauseMin, drone.WanderPauseMax);
    }

    private MapCoordinates? PickWanderTarget(EntityUid uid, CargoDeliveryDroneComponent drone)
    {
        var home = GetHome(uid, drone);

        for (var i = 0; i < 6; i++)
        {
            var offset = _random.NextAngle().RotateVec(new Vector2(_random.NextFloat(0.5f, drone.WanderRadius), 0f));
            var candidate = new MapCoordinates(home.Position + offset, home.MapId);
            var coords = _transform.ToCoordinates(candidate);

            if (_turf.GetTileRef(coords) is not { } tile || tile.Tile.IsEmpty)
                continue;

            if (_turf.IsTileBlocked(tile, CollisionGroup.Impassable))
                continue;

            return candidate;
        }

        return home;
    }

    #endregion

    private void UpdateReturning(EntityUid uid, CargoDeliveryDroneComponent drone, TransformComponent xform,
        PhysicsComponent body, float frameTime)
    {
        drone.FlightTime += frameTime;
        TrackSafePosition(uid, drone, xform, frameTime);

        if (drone.Crate != null && drone.Pad is { } pad && !TerminatingOrDeleted(pad) && Transform(pad).MapID == xform.MapID)
        {
            if (drone.FlightTime > drone.MaxFlightTime)
            {
                ReturnCrate(uid, drone);
                return;
            }

            if (MoveTowards(uid, drone, xform, body, _transform.GetWorldPosition(pad), drone.ReturnSpeed) <= drone.ArriveRange)
            {
                ReturnCrate(uid, drone);
                drone.FlightTime = 0f;
            }

            return;
        }

        if (drone.Crate != null)
            ReturnCrate(uid, drone);

        var home = GetHome(uid, drone);
        if (home.MapId != xform.MapID || drone.FlightTime > drone.MaxFlightTime)
        {
            drone.Home = Transform(uid).Coordinates;
            SetIdle(uid, drone);
            return;
        }

        if (MoveTowards(uid, drone, xform, body, home.Position, drone.ReturnSpeed) <= drone.ArriveRange)
            SetIdle(uid, drone);
    }

    private bool ValidatePoint(CargoDeliveryDroneComponent drone, TransformComponent xform, out string reason)
    {
        reason = string.Empty;

        if (drone.Point is not { } point || TerminatingOrDeleted(point) || !HasComp<CargoDeliveryPointComponent>(point))
        {
            reason = "cargo-delivery-abort-point-gone";
            return false;
        }

        var pointXform = Transform(point);
        if (!pointXform.Anchored)
        {
            reason = "cargo-delivery-abort-point-moved";
            return false;
        }

        if (pointXform.MapID != xform.MapID)
        {
            reason = "cargo-delivery-abort-point-gone";
            return false;
        }

        return true;
    }

    private MapCoordinates GetHome(EntityUid uid, CargoDeliveryDroneComponent drone)
    {
        if (drone.Home is { } home && Exists(home.EntityId) && !TerminatingOrDeleted(home.EntityId))
            return _transform.ToMapCoordinates(home);

        return _transform.GetMapCoordinates(uid);
    }

    private bool TryPickUp(EntityUid uid, CargoDeliveryDroneComponent drone)
    {
        if (drone.PendingCrate is not { } crate || drone.Pad is not { } pad || TerminatingOrDeleted(pad))
            return false;

        if (!IsCrateOnPad(pad, crate))
            return false;

        var terminalComp = drone.Terminal is { } terminal ? CompOrNull<CargoDeliveryTerminalComponent>(terminal) : null;
        if (CheckContents(crate, terminalComp?.ContentsBlacklist, 0) != CargoDeliveryPadStatus.Ready)
            return false;

        var slot = _container.EnsureContainer<ContainerSlot>(uid, drone.ContainerId);
        if (!_container.Insert(crate, slot))
            return false;

        drone.Crate = crate;
        drone.PendingCrate = null;
        return true;
    }

    private float MoveTowards(EntityUid uid, CargoDeliveryDroneComponent drone, TransformComponent xform,
        PhysicsComponent body, Vector2 target, float speed)
    {
        var pos = _transform.GetWorldPosition(xform);
        var delta = target - pos;
        var dist = delta.Length();

        if (dist < 0.05f)
        {
            StopMoving(uid, body);
            return dist;
        }

        var dir = delta / dist;
        var velocity = dir * MathF.Min(speed, dist * 4f);

        var parentRot = xform.ParentUid.IsValid() ? _transform.GetWorldRotation(xform.ParentUid) : Angle.Zero;
        _physics.SetLinearVelocity(uid, (-parentRot).RotateVec(velocity), body: body);

        var angle = dir.ToWorldAngle();
        if (Math.Abs(Angle.ShortestDistance(drone.LastRotation, angle).Theta) > 0.3)
        {
            drone.LastRotation = angle;
            _transform.SetWorldRotation(uid, angle);
        }

        return dist;
    }

    private void StopMoving(EntityUid uid, PhysicsComponent? body = null)
    {
        if (!Resolve(uid, ref body, false))
            return;

        if (body.LinearVelocity != Vector2.Zero)
            _physics.SetLinearVelocity(uid, Vector2.Zero, body: body);
    }

    private void TrackSafePosition(EntityUid uid, CargoDeliveryDroneComponent drone, TransformComponent xform, float frameTime)
    {
        drone.SafeCheckAccumulator += frameTime;
        if (drone.SafeCheckAccumulator < SafeCheckInterval)
            return;

        drone.SafeCheckAccumulator = 0f;

        var coords = _transform.GetMoverCoordinates(uid, xform);
        if (_turf.GetTileRef(coords) is not { } tile)
        {
            drone.LastSafeCoordinates = _transform.ToMapCoordinates(coords);
            return;
        }

        if (!_turf.IsTileBlocked(tile, CollisionGroup.Impassable))
            drone.LastSafeCoordinates = _transform.ToMapCoordinates(coords);
    }

    private void Deliver(EntityUid uid, CargoDeliveryDroneComponent drone)
    {
        var point = drone.Point!.Value;
        var pointComp = Comp<CargoDeliveryPointComponent>(point);
        var terminalComp = drone.Terminal is { } terminal ? CompOrNull<CargoDeliveryTerminalComponent>(terminal) : null;

        if (drone.Crate is { } crate && !TerminatingOrDeleted(crate))
        {
            _container.RemoveEntity(uid, crate);
            _transform.SetCoordinates(crate, Transform(point).Coordinates);
            _transform.AttachToGridOrMap(crate);
        }

        drone.Crate = null;

        if (terminalComp != null)
        {
            ChangeBalance(drone.Terminal!.Value, terminalComp.Account, terminalComp.Reward);
            terminalComp.TotalDelivered++;

            SendRadio(drone.Terminal!.Value, terminalComp.RadioChannel, Loc.GetString("cargo-delivery-radio-delivered",
                ("point", drone.DestinationName), ("reward", terminalComp.Reward)));
        }

        if (pointComp.RadioChannel is { } channel)
        {
            var location = FormattedMessage.RemoveMarkupPermissive(_navMap.GetNearestBeaconString(point));
            SendRadio(uid, channel, Loc.GetString("cargo-delivery-radio-department-arrived",
                ("point", drone.DestinationName),
                ("location", location)));
        }

        _popup.PopupEntity(Loc.GetString("cargo-delivery-point-arrived"), point, PopupType.Medium);
        _audio.PlayPvs(drone.DeliverSound, uid);

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"Cargo delivery drone {ToPrettyString(uid):drone} delivered a crate to {ToPrettyString(point):target}");

        ClearPointTarget(drone);
        drone.State = CargoDeliveryDroneState.Returning;
        drone.FlightTime = 0f;
        UpdateDroneVisuals(uid, drone);
    }

    private void Abort(EntityUid uid, CargoDeliveryDroneComponent drone, string reasonLoc)
    {
        if (drone.Terminal is { } terminal && TryComp<CargoDeliveryTerminalComponent>(terminal, out var terminalComp))
        {
            if (drone.Point is { } point)
                terminalComp.PointCooldowns.Remove(point);

            SendRadio(terminal, terminalComp.RadioChannel, Loc.GetString("cargo-delivery-radio-aborted",
                ("point", drone.DestinationName), ("reason", Loc.GetString(reasonLoc))));
        }

        ReleasePendingCrate(drone);
        ClearPointTarget(drone);

        drone.State = CargoDeliveryDroneState.Returning;
        drone.FlightTime = 0f;
        UpdateDroneVisuals(uid, drone);
    }

    private void ReleasePendingCrate(CargoDeliveryDroneComponent drone)
    {
        if (drone.PendingCrate is not { } crate)
            return;

        drone.PendingCrate = null;
        if (drone.Pad is not { } pad || TerminatingOrDeleted(pad) || !IsCrateOnPad(pad, crate))
            return;

        RemComp<CargoDeliveryLockComponent>(crate);
        _lock.Unlock(crate, null);
    }

    private void ReturnCrate(EntityUid uid, CargoDeliveryDroneComponent drone)
    {
        if (drone.Crate is not { } crate)
            return;

        drone.Crate = null;
        if (TerminatingOrDeleted(crate))
            return;

        _container.RemoveEntity(uid, crate);
        var coords = drone.Pad is { } pad && !TerminatingOrDeleted(pad)
            ? Transform(pad).Coordinates
            : _transform.GetMoverCoordinates(uid);

        _transform.SetCoordinates(crate, coords);
        _transform.AttachToGridOrMap(crate);

        RemComp<CargoDeliveryLockComponent>(crate);
        _lock.Unlock(crate, null);

        if (drone.Terminal is { } terminal && TryComp<CargoDeliveryTerminalComponent>(terminal, out var terminalComp))
        {
            SendRadio(terminal, terminalComp.RadioChannel,
                Loc.GetString("cargo-delivery-radio-returned-crate", ("point", drone.DestinationName)));
        }

        UpdateDroneVisuals(uid, drone);
    }

    private void SetIdle(EntityUid uid, CargoDeliveryDroneComponent drone)
    {
        StopMoving(uid);

        if (drone.Terminal is { } terminal && TryComp<CargoDeliveryTerminalComponent>(terminal, out var terminalComp))
            terminalComp.ActiveDrones.Remove(uid);

        ReleasePendingCrate(drone);
        ClearPointTarget(drone);

        drone.State = CargoDeliveryDroneState.Idle;
        drone.Terminal = null;
        drone.Pad = null;
        drone.DestinationName = string.Empty;
        drone.WanderTarget = null;
        drone.WanderTimer = _random.NextFloat(drone.WanderPauseMin, drone.WanderPauseMax);
        drone.FlightTime = 0f;
        drone.Penalized = false;
        UpdateDroneVisuals(uid, drone);
    }

    #region Point target (анимация метки)

    private void SetPointTarget(Entity<CargoDeliveryDroneComponent> drone, Entity<CargoDeliveryPointComponent> point)
    {
        ClearPointTarget(drone.Comp);
        drone.Comp.Point = point;
        point.Comp.Incoming++;
        _appearance.SetData(point, CargoDeliveryPointVisuals.Incoming, true);
    }

    private void ClearPointTarget(CargoDeliveryDroneComponent drone)
    {
        if (drone.Point is not { } point)
            return;

        drone.Point = null;
        if (TerminatingOrDeleted(point) || !TryComp<CargoDeliveryPointComponent>(point, out var pointComp))
            return;

        pointComp.Incoming = Math.Max(0, pointComp.Incoming - 1);
        _appearance.SetData(point, CargoDeliveryPointVisuals.Incoming, pointComp.Incoming > 0);
    }

    #endregion

    #region Damage

    private void OnDroneDamaged(EntityUid uid, CargoDeliveryDroneComponent drone, DamageChangedEvent args)
    {
        if (!args.DamageIncreased || drone.State == CargoDeliveryDroneState.Downed)
            return;

        if (args.Damageable.TotalDamage < FixedPoint2.New(drone.DownThreshold))
            return;

        KnockDown(uid, drone, args.Origin);
    }

    private void OnDroneEmp(EntityUid uid, CargoDeliveryDroneComponent drone, ref EmpPulseEvent args)
    {
        args.Affected = true;
        args.Disabled = true;

        if (drone.State != CargoDeliveryDroneState.Downed)
            KnockDown(uid, drone, null);
    }

    private void KnockDown(EntityUid uid, CargoDeliveryDroneComponent drone, EntityUid? origin)
    {
        var hadCrate = drone.Crate != null;
        var terminalComp = drone.Terminal is { } terminal ? CompOrNull<CargoDeliveryTerminalComponent>(terminal) : null;

        if (drone.State is CargoDeliveryDroneState.ToPad or CargoDeliveryDroneState.Loading)
        {
            if (terminalComp != null && drone.Point is { } point)
                terminalComp.PointCooldowns.Remove(point);
            ReleasePendingCrate(drone);
        }

        ClearPointTarget(drone);

        drone.State = CargoDeliveryDroneState.Downed;
        drone.StateTimer = drone.DownedDuration;
        drone.WanderTarget = null;
        StopMoving(uid);

        if (drone.LastSafeCoordinates is { } safe && IsOverBlockedTile(uid))
        {
            _transform.SetMapCoordinates(uid, safe);
            _transform.AttachToGridOrMap(uid);
        }

        DropCrate(uid, drone);

        if (hadCrate && !drone.Penalized && terminalComp != null)
        {
            drone.Penalized = true;
            ChangeBalance(drone.Terminal!.Value, terminalComp.Account, -terminalComp.Penalty);
            terminalComp.TotalFailed++;

            var location = FormattedMessage.RemoveMarkupPermissive(_navMap.GetNearestBeaconString(uid));
            SendRadio(drone.Terminal!.Value, terminalComp.RadioChannel, Loc.GetString("cargo-delivery-radio-downed",
                ("point", drone.DestinationName),
                ("location", location),
                ("penalty", terminalComp.Penalty)));
        }

        _popup.PopupEntity(Loc.GetString("cargo-delivery-drone-downed"), uid, PopupType.MediumCaution);
        _audio.PlayPvs(drone.DownSound, uid);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"Cargo delivery drone {ToPrettyString(uid):drone} was shot down by {ToPrettyString(origin):user}");

        UpdateDroneVisuals(uid, drone);
    }

    private bool IsOverBlockedTile(EntityUid uid)
    {
        var coords = _transform.GetMoverCoordinates(uid);
        return _turf.GetTileRef(coords) is { } tile && _turf.IsTileBlocked(tile, CollisionGroup.Impassable);
    }

    private void DropCrate(EntityUid uid, CargoDeliveryDroneComponent drone)
    {
        if (drone.Crate is not { } crate)
            return;

        drone.Crate = null;
        if (TerminatingOrDeleted(crate))
            return;

        _container.RemoveEntity(uid, crate);
        _transform.SetCoordinates(crate, _transform.GetMoverCoordinates(uid));
        _transform.AttachToGridOrMap(crate);
    }

    private void Reboot(EntityUid uid, CargoDeliveryDroneComponent drone)
    {
        if (TryComp<DamageableComponent>(uid, out var damageable))
            _damageable.SetAllDamage(uid, damageable, FixedPoint2.Zero);

        drone.State = CargoDeliveryDroneState.Returning;
        drone.FlightTime = 0f;

        _popup.PopupEntity(Loc.GetString("cargo-delivery-drone-reboot"), uid, PopupType.Medium);
        _audio.PlayPvs(drone.RebootSound, uid);
        UpdateDroneVisuals(uid, drone);
    }

    private void OnDroneTerminating(EntityUid uid, CargoDeliveryDroneComponent drone, ref EntityTerminatingEvent args)
    {
        if (drone.Terminal is { } terminal && TryComp<CargoDeliveryTerminalComponent>(terminal, out var terminalComp))
            terminalComp.ActiveDrones.Remove(uid);

        ReleasePendingCrate(drone);
        ClearPointTarget(drone);

        if (drone.Crate is not { } crate || TerminatingOrDeleted(crate))
            return;

        var parent = Transform(uid).ParentUid;
        if (!parent.IsValid() || TerminatingOrDeleted(parent))
            return;

        DropCrate(uid, drone);
    }

    #endregion

    private void UpdateDroneVisuals(EntityUid uid, CargoDeliveryDroneComponent drone)
    {
        var state = drone.State == CargoDeliveryDroneState.Downed
            ? CargoDeliveryDroneVisualState.Downed
            : drone.Crate != null
                ? CargoDeliveryDroneVisualState.Carrying
                : CargoDeliveryDroneVisualState.Flying;

        _appearance.SetData(uid, CargoDeliveryDroneVisuals.State, state);
    }
}
