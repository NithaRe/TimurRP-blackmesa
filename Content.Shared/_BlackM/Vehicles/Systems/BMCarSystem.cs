using System.Numerics;
using Content.Shared._BlackM.Vehicles.Components;
using Content.Shared.Access.Components;
using Content.Shared.Actions;
using Content.Shared.Camera;
using Content.Shared.Damage;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._BlackM.Vehicles.Systems;

public sealed class BMCarSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPointLightSystem _light = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _recoil = default!;

    private static readonly SpriteSpecifier EnterIcon =
        new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/in.svg.192dpi.png"));

    private static readonly SpriteSpecifier EjectIcon =
        new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/eject.svg.192dpi.png"));

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BMCarComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BMCarComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<BMCarComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<BMCarComponent, CanDropTargetEvent>(OnCanDrop);
        SubscribeLocalEvent<BMCarComponent, DragDropTargetEvent>(OnDragDrop);
        SubscribeLocalEvent<BMCarComponent, BMCarEnterDoAfterEvent>(OnEnterDoAfter);
        SubscribeLocalEvent<BMCarComponent, BMCarPullOutDoAfterEvent>(OnPullOutDoAfter);
        SubscribeLocalEvent<BMCarComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<BMCarComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<BMCarComponent, DestructionEventArgs>(OnDestruction);
        SubscribeLocalEvent<BMCarComponent, EntityTerminatingEvent>(OnTerminating);
        SubscribeLocalEvent<BMCarComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<BMCarComponent, GetAdditionalAccessEvent>(OnGetAdditionalAccess);

        SubscribeLocalEvent<BMCarComponent, BMCarEngineActionEvent>(OnEngineAction);
        SubscribeLocalEvent<BMCarComponent, BMCarHornActionEvent>(OnHornAction);
        SubscribeLocalEvent<BMCarComponent, BMCarSirenActionEvent>(OnSirenAction);
        SubscribeLocalEvent<BMCarComponent, BMCarLightsActionEvent>(OnLightsAction);
        SubscribeLocalEvent<BMCarComponent, BMCarControlModeActionEvent>(OnControlModeAction);
        SubscribeLocalEvent<BMCarComponent, BMCarExitActionEvent>(OnExitAction);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<BMCarComponent>();
        while (query.MoveNext(out var uid, out var car))
        {
            if (car.Engine != BMCarEngineState.Starting || now < car.EngineReadyAt)
                continue;

            car.Engine = BMCarEngineState.Running;
            Dirty(uid, car);

            if (_net.IsServer && car.DriverSlot.ContainedEntity is { } driver)
                _popup.PopupEntity(Loc.GetString("bm-car-engine-running"), uid, driver);
        }
    }

    #region Helpers

    public EntityUid? GetDriver(BMCarComponent car)
    {
        return car.DriverSlot?.ContainedEntity;
    }

    public bool IsOccupant(BMCarComponent car, EntityUid who)
    {
        return car.DriverSlot?.ContainedEntity == who || car.Passengers?.Contains(who) == true;
    }

    public bool HasOccupants(BMCarComponent car)
    {
        return car.DriverSlot?.ContainedEntity != null || car.Passengers?.ContainedEntities.Count > 0;
    }

    private List<EntityUid> GetOccupants(BMCarComponent car)
    {
        var list = new List<EntityUid>();
        if (car.DriverSlot?.ContainedEntity is { } driver)
            list.Add(driver);
        if (car.Passengers != null)
            list.AddRange(car.Passengers.ContainedEntities);
        return list;
    }

    #endregion

    #region Seats

    private void OnStartup(Entity<BMCarComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.DriverSlot = _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.DriverSlotId);
        ent.Comp.Passengers = _container.EnsureContainer<Container>(ent, ent.Comp.PassengerContainerId);

        if (_light.TryGetLight(ent, out var light))
            _light.SetEnabled(ent, ent.Comp.Headlights, comp: light);
    }

    public bool CanEnter(Entity<BMCarComponent> car, EntityUid who, bool driver, EntityUid? popupTo = null)
    {
        if (!HasComp<MobStateComponent>(who) || IsOccupant(car.Comp, who) || _container.IsEntityInContainer(who))
            return false;

        BaseContainer container;
        if (driver)
        {
            if (car.Comp.DriverSlot.ContainedEntity != null)
            {
                if (popupTo != null)
                    _popup.PopupClient(Loc.GetString("bm-car-driver-taken"), car, popupTo.Value);
                return false;
            }

            if (!_mobState.IsAlive(who))
                return false;

            container = car.Comp.DriverSlot;
        }
        else
        {
            if (car.Comp.Passengers.ContainedEntities.Count >= car.Comp.PassengerSeats)
            {
                if (popupTo != null)
                    _popup.PopupClient(Loc.GetString("bm-car-no-seats"), car, popupTo.Value);
                return false;
            }

            container = car.Comp.Passengers;
        }

        return _container.CanInsert(who, container);
    }

    public bool TryStartEnter(Entity<BMCarComponent> car, EntityUid user, EntityUid who, bool driver)
    {
        if (!CanEnter(car, who, driver, user))
            return false;

        var delay = user == who ? car.Comp.EnterDelay : car.Comp.PutInDelay;
        var args = new DoAfterArgs(EntityManager, user, delay, new BMCarEnterDoAfterEvent(driver), car.Owner, target: who)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        };

        if (!_doAfter.TryStartDoAfter(args))
            return false;

        if (user != who && _net.IsServer)
            _popup.PopupEntity(Loc.GetString("bm-car-being-put-in", ("user", user)), who, who, PopupType.MediumCaution);

        return true;
    }

    public bool TryEnter(Entity<BMCarComponent> car, EntityUid who, bool driver)
    {
        if (!CanEnter(car, who, driver))
            return false;

        BaseContainer container = driver ? car.Comp.DriverSlot : car.Comp.Passengers;
        if (!_container.Insert(who, container))
            return false;

        _audio.PlayPredicted(car.Comp.DoorSound, car, who);
        return true;
    }

    public bool TryExit(Entity<BMCarComponent> car, EntityUid who, EntityUid? user = null)
    {
        BaseContainer container;
        bool driver;
        if (car.Comp.DriverSlot.ContainedEntity == who)
        {
            container = car.Comp.DriverSlot;
            driver = true;
        }
        else if (car.Comp.Passengers.Contains(who))
        {
            container = car.Comp.Passengers;
            driver = false;
        }
        else
        {
            return false;
        }

        var destination = FindExitPosition(car, driver);
        if (!_container.Remove(who, container))
            return false;

        if (destination != null)
            _transform.SetWorldPosition(who, destination.Value);

        _audio.PlayPredicted(car.Comp.DoorSound, car, user ?? who);
        return true;
    }

    public void EjectAll(Entity<BMCarComponent> car, EntityUid? user = null)
    {
        foreach (var who in GetOccupants(car.Comp))
        {
            TryExit(car, who, user);
        }
    }

    private Vector2? FindExitPosition(Entity<BMCarComponent> car, bool driver)
    {
        var d = car.Comp.ExitDistance;
        var left = new Vector2(d, 0f);
        var right = new Vector2(-d, 0f);
        var front = new Vector2(0f, -1.3f);
        var back = new Vector2(0f, 1.3f);
        var offsets = driver
            ? new[] { left, right, back, front }
            : new[] { right, left, back, front };

        foreach (var offset in offsets)
        {
            var coords = new EntityCoordinates(car, offset);
            if (!_turf.TryGetTileRef(coords, out var tile) || tile.Value.Tile.IsEmpty)
                continue;

            if (_turf.IsTileBlocked(tile.Value, CollisionGroup.Impassable))
                continue;

            return _transform.ToMapCoordinates(coords).Position;
        }

        return null;
    }

    private void OnGetVerbs(Entity<BMCarComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        if (IsOccupant(ent.Comp, user))
            return;

        if (ent.Comp.DriverSlot.ContainedEntity == null && _mobState.IsAlive(user))
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("bm-car-verb-drive"),
                Icon = EnterIcon,
                Priority = 3,
                Act = () => TryStartEnter(ent, user, user, true),
            });
        }

        if (ent.Comp.Passengers.ContainedEntities.Count < ent.Comp.PassengerSeats)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("bm-car-verb-ride"),
                Icon = EnterIcon,
                Priority = 2,
                Act = () => TryStartEnter(ent, user, user, false),
            });
        }

        if (HasOccupants(ent.Comp))
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("bm-car-verb-pull-out"),
                Icon = EjectIcon,
                Priority = -1,
                Act = () => TryStartPullOut(ent, user),
            });
        }
    }

    private void OnActivate(Entity<BMCarComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || IsOccupant(ent.Comp, args.User))
            return;

        var driver = ent.Comp.DriverSlot.ContainedEntity == null && _mobState.IsAlive(args.User);
        args.Handled = TryStartEnter(ent, args.User, args.User, driver);
    }

    private void OnCanDrop(Entity<BMCarComponent> ent, ref CanDropTargetEvent args)
    {
        args.Handled = true;
        var self = args.Dragged == args.User;
        args.CanDrop |= (self && CanEnter(ent, args.Dragged, true)) || CanEnter(ent, args.Dragged, false);
    }

    private void OnDragDrop(Entity<BMCarComponent> ent, ref DragDropTargetEvent args)
    {
        if (args.Handled)
            return;

        var self = args.Dragged == args.User;
        var driver = self && ent.Comp.DriverSlot.ContainedEntity == null && _mobState.IsAlive(args.User);
        args.Handled = TryStartEnter(ent, args.User, args.Dragged, driver);
    }

    private void OnEnterDoAfter(Entity<BMCarComponent> ent, ref BMCarEnterDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        TryEnter(ent, args.Target ?? args.User, args.Driver);
    }

    public void TryStartPullOut(Entity<BMCarComponent> car, EntityUid user)
    {
        if (!HasOccupants(car.Comp))
            return;

        var args = new DoAfterArgs(EntityManager, user, car.Comp.PullOutDelay, new BMCarPullOutDoAfterEvent(), car.Owner, target: car.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        };

        if (!_doAfter.TryStartDoAfter(args) || !_net.IsServer)
            return;

        foreach (var who in GetOccupants(car.Comp))
        {
            _popup.PopupEntity(Loc.GetString("bm-car-being-pulled-out", ("user", user)), who, who, PopupType.MediumCaution);
        }
    }

    private void OnPullOutDoAfter(Entity<BMCarComponent> ent, ref BMCarPullOutDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        EjectAll(ent, args.User);
    }

    private void OnInserted(Entity<BMCarComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (_timing.ApplyingState)
            return;

        var who = args.Entity;
        var driver = args.Container.ID == ent.Comp.DriverSlotId;
        if (!driver && args.Container.ID != ent.Comp.PassengerContainerId)
            return;

        if (driver)
            _mover.SetRelay(who, ent);

        if (!_net.IsServer)
            return;

        AddOccupantActions(ent, who, driver);
        _popup.PopupEntity(Loc.GetString(driver ? "bm-car-entered-driver" : "bm-car-entered-passenger"), who, who);
    }

    private void OnRemoved(Entity<BMCarComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (_timing.ApplyingState)
            return;

        var who = args.Entity;
        var driver = args.Container.ID == ent.Comp.DriverSlotId;
        if (!driver && args.Container.ID != ent.Comp.PassengerContainerId)
            return;

        if (driver)
        {
            RemComp<RelayInputMoverComponent>(who);
            if (ent.Comp.Engine != BMCarEngineState.Off)
                SetEngine(ent, BMCarEngineState.Off, null);

            if (ent.Comp.Siren)
            {
                ent.Comp.Siren = false;
                Dirty(ent);
            }
        }

        if (_net.IsServer)
            RemoveOccupantActions(ent, who);
    }

    private void AddOccupantActions(Entity<BMCarComponent> ent, EntityUid who, bool driver)
    {
        RemoveOccupantActions(ent, who);

        var list = new List<EntityUid>();
        if (driver)
        {
            AddAction(ent, who, ent.Comp.EngineAction, list);
            AddAction(ent, who, ent.Comp.HornAction, list);
            AddAction(ent, who, ent.Comp.SirenAction, list);
            AddAction(ent, who, ent.Comp.LightsAction, list);
            AddAction(ent, who, ent.Comp.ControlModeAction, list);
        }

        AddAction(ent, who, ent.Comp.ExitAction, list);
        ent.Comp.OccupantActions[who] = list;
    }

    private void AddAction(Entity<BMCarComponent> ent, EntityUid who, EntProtoId? proto, List<EntityUid> list)
    {
        if (proto == null)
            return;

        string protoId = proto.Value;
        var action = _actions.AddAction(who, protoId, ent.Owner);
        if (action != null)
            list.Add(action.Value);
    }

    private void RemoveOccupantActions(Entity<BMCarComponent> ent, EntityUid who)
    {
        _actions.RemoveProvidedActions(who, ent.Owner);

        if (!ent.Comp.OccupantActions.Remove(who, out var list))
            return;

        foreach (var action in list)
        {
            QueueDel(action);
        }
    }

    private void OnDestruction(Entity<BMCarComponent> ent, ref DestructionEventArgs args)
    {
        EjectAll(ent);
    }

    private void OnTerminating(Entity<BMCarComponent> ent, ref EntityTerminatingEvent args)
    {
        // On the client the car may just leave PVS.
        if (_net.IsClient)
            return;

        EjectAll(ent);
    }

    private void OnGetAdditionalAccess(Entity<BMCarComponent> ent, ref GetAdditionalAccessEvent args)
    {
        if (ent.Comp.DriverSlot.ContainedEntity is { } driver)
            args.Entities.Add(driver);
    }

    #endregion

    #region Engine & actions

    private void SetEngine(Entity<BMCarComponent> ent, BMCarEngineState state, EntityUid? user)
    {
        ent.Comp.Engine = state;
        if (state == BMCarEngineState.Starting)
            ent.Comp.EngineReadyAt = _timing.CurTime + ent.Comp.IgnitionTime;

        Dirty(ent);
    }

    private void OnEngineAction(Entity<BMCarComponent> ent, ref BMCarEngineActionEvent args)
    {
        if (args.Handled || ent.Comp.DriverSlot.ContainedEntity != args.Performer)
            return;

        args.Handled = true;
        var user = args.Performer;

        if (ent.Comp.Engine == BMCarEngineState.Off)
        {
            SetEngine(ent, BMCarEngineState.Starting, user);
            _audio.PlayPredicted(ent.Comp.StartSound, ent, user);
            _popup.PopupClient(Loc.GetString("bm-car-engine-starting"), ent, user);
        }
        else
        {
            SetEngine(ent, BMCarEngineState.Off, user);
            _audio.PlayPredicted(ent.Comp.StopSound, ent, user);
            _popup.PopupClient(Loc.GetString("bm-car-engine-off"), ent, user);
        }
    }

    private void OnHornAction(Entity<BMCarComponent> ent, ref BMCarHornActionEvent args)
    {
        if (args.Handled || ent.Comp.DriverSlot.ContainedEntity != args.Performer)
            return;

        args.Handled = true;
        _audio.PlayPredicted(ent.Comp.HornSound, ent, args.Performer);
    }

    private void OnSirenAction(Entity<BMCarComponent> ent, ref BMCarSirenActionEvent args)
    {
        if (args.Handled || ent.Comp.DriverSlot.ContainedEntity != args.Performer)
            return;

        args.Handled = true;
        ent.Comp.Siren = !ent.Comp.Siren;
        Dirty(ent);
    }

    private void OnLightsAction(Entity<BMCarComponent> ent, ref BMCarLightsActionEvent args)
    {
        if (args.Handled || ent.Comp.DriverSlot.ContainedEntity != args.Performer)
            return;

        args.Handled = true;
        ent.Comp.Headlights = !ent.Comp.Headlights;
        Dirty(ent);

        if (_light.TryGetLight(ent, out var light))
            _light.SetEnabled(ent, ent.Comp.Headlights, comp: light);

        _audio.PlayPredicted(ent.Comp.LightsSound, ent, args.Performer);
    }

    private void OnControlModeAction(Entity<BMCarComponent> ent, ref BMCarControlModeActionEvent args)
    {
        if (args.Handled || ent.Comp.DriverSlot.ContainedEntity != args.Performer)
            return;

        args.Handled = true;
        ent.Comp.TankControls = !ent.Comp.TankControls;
        Dirty(ent);

        var msg = ent.Comp.TankControls ? "bm-car-control-car" : "bm-car-control-screen";
        _popup.PopupClient(Loc.GetString(msg), ent, args.Performer);
    }

    private void OnExitAction(Entity<BMCarComponent> ent, ref BMCarExitActionEvent args)
    {
        if (args.Handled || !IsOccupant(ent.Comp, args.Performer))
            return;

        args.Handled = TryExit(ent, args.Performer);
    }

    #endregion

    #region Impacts

    private void OnStartCollide(Entity<BMCarComponent> ent, ref StartCollideEvent args)
    {
        if (_net.IsClient)
            return;

        if (args.OurFixtureId == ent.Comp.BumperFixtureId)
        {
            HandleRam(ent, args.OurBody, args.OtherEntity);
            return;
        }

        if (!args.OurFixture.Hard || !args.OtherFixture.Hard)
            return;

        if (args.OtherBody.BodyType == BodyType.Static || HasComp<BMCarComponent>(args.OtherEntity))
            HandleCrash(ent, args.OurBody, args.WorldNormal);
    }

    private void HandleRam(Entity<BMCarComponent> ent, PhysicsComponent body, EntityUid victim)
    {
        if (!HasComp<MobStateComponent>(victim) || IsOccupant(ent.Comp, victim))
            return;

        var velocity = body.LinearVelocity;
        var speed = velocity.Length();
        if (speed < ent.Comp.MinRamSpeed)
            return;

        var moveDir = velocity / speed;
        var toVictim = _transform.GetWorldPosition(victim) - _transform.GetWorldPosition(ent);
        var dir = toVictim.LengthSquared() > 0.0001f ? Vector2.Normalize(toVictim) : moveDir;

        if (Vector2.Dot(velocity, dir) < ent.Comp.MinRamSpeed * 0.5f)
            return;

        var now = _timing.CurTime;
        if (ent.Comp.LastHit.TryGetValue(victim, out var next) && now < next)
            return;

        CleanupHits(ent.Comp, now);
        ent.Comp.LastHit[victim] = now + ent.Comp.RamCooldown;

        var driver = ent.Comp.DriverSlot.ContainedEntity;
        var origin = driver ?? ent.Owner;
        var power = Math.Clamp(speed / Math.Max(ent.Comp.MaxSpeed, 0.1f), 0f, 1.5f);

        if (_standing.IsDown(victim))
        {
            _damageable.TryChangeDamage(victim, ent.Comp.RunOverDamage * (0.5f + power), origin: origin);
            _audio.PlayPvs(ent.Comp.RunOverSound, victim);
            _popup.PopupEntity(Loc.GetString("bm-car-run-over", ("car", ent.Owner), ("victim", victim)), victim, PopupType.MediumCaution);
            _physics.SetLinearVelocity(ent, velocity * 0.9f, body: body);
        }
        else
        {
            _damageable.TryChangeDamage(victim, ent.Comp.RamDamage * speed, origin: origin);
            _stun.TryKnockdown(victim, TimeSpan.FromSeconds(ent.Comp.RamKnockdown * Math.Max(power, 0.4f)), true);

            var throwDir = moveDir * 0.65f + dir * 0.35f;
            throwDir = throwDir.LengthSquared() > 0.0001f ? Vector2.Normalize(throwDir) : moveDir;
            _throwing.TryThrow(victim,
                throwDir * (speed * ent.Comp.RamThrowFactor + 0.5f),
                baseThrowSpeed: speed + 3f,
                user: driver,
                pushbackRatio: 0f,
                recoil: false,
                playSound: false);

            _audio.PlayPvs(ent.Comp.RamSound, ent);
            _popup.PopupEntity(Loc.GetString("bm-car-ram", ("car", ent.Owner), ("victim", victim)), victim, PopupType.MediumCaution);
            _physics.SetLinearVelocity(ent, velocity * ent.Comp.RamSlowdown, body: body);
        }

        KickOccupants(ent.Comp, -moveDir * (0.3f + 0.4f * power));
    }

    private void HandleCrash(Entity<BMCarComponent> ent, PhysicsComponent body, Vector2 normal)
    {
        var velocity = body.LinearVelocity;
        var total = velocity.Length();
        if (total < 0.01f)
            return;

        // Only the speed into the wall counts, scraping is not a crash.
        var speed = normal.LengthSquared() > 0.0001f
            ? Math.Abs(Vector2.Dot(velocity, Vector2.Normalize(normal)))
            : total;
        if (speed < ent.Comp.CrashSpeed)
            return;

        var now = _timing.CurTime;
        if (now < ent.Comp.NextCrash)
            return;

        ent.Comp.NextCrash = now + TimeSpan.FromSeconds(0.8);

        var over = speed - ent.Comp.CrashSpeed + 1f;
        _damageable.TryChangeDamage(ent.Owner, ent.Comp.CrashDamage * speed);

        foreach (var who in GetOccupants(ent.Comp))
        {
            _damageable.TryChangeDamage(who, ent.Comp.OccupantCrashDamage * over, origin: ent.Owner);
        }

        KickOccupants(ent.Comp, -velocity / total * Math.Min(1.5f, 0.5f + over * 0.25f));
        _audio.PlayPvs(ent.Comp.CrashSound, ent);

        if (ent.Comp.DriverSlot.ContainedEntity is { } driver)
            _popup.PopupEntity(Loc.GetString("bm-car-crash"), ent, driver, PopupType.LargeCaution);
    }

    private void KickOccupants(BMCarComponent car, Vector2 kick)
    {
        foreach (var who in GetOccupants(car))
        {
            _recoil.KickCamera(who, kick);
        }
    }

    private static void CleanupHits(BMCarComponent car, TimeSpan now)
    {
        if (car.LastHit.Count < 16)
            return;

        var expired = new List<EntityUid>();
        foreach (var (uid, time) in car.LastHit)
        {
            if (time <= now)
                expired.Add(uid);
        }

        foreach (var uid in expired)
        {
            car.LastHit.Remove(uid);
        }
    }

    #endregion
}
