using System.Linq;
using Content.Server.Electrocution;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Light.Components;
using Content.Server.Light.EntitySystems;
using Content.Server.Pinpointer;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Tools;
using Content.Server.Wires;
using Content.Shared._BlackM.EngineeringBreakdown;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Examine;
using Content.Shared.Ghost;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._BlackM.EngineeringBreakdown;

public sealed partial class BreakdownSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ApcSystem _apc = default!;
    [Dependency] private PoweredLightSystem _light = default!;
    [Dependency] private PowerReceiverSystem _receiver = default!;
    [Dependency] private SharedDoorSystem _door = default!;
    [Dependency] private ToolSystem _tool = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ElectrocutionSystem _electrocution = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedJobSystem _job = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private NavMapSystem _navMap = default!;

    private static readonly ProtoId<DepartmentPrototype> EngineeringDepartment = "Engineering";
    private static readonly SoundSpecifier RepairedSound = new SoundPathSpecifier("/Audio/Machines/machine_switch.ogg");
    private static readonly SoundSpecifier StepSound = new SoundPathSpecifier("/Audio/Items/ratchet.ogg");

    private const float NavigatorUpdateInterval = 1f;
    private float _navigatorAccumulator;

    private readonly List<RepairMarker> _markers = new();
    private readonly HashSet<EntityUid> _navRecipients = new();
    private readonly List<EntityUid> _navToRemove = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BreakdownComponent, InteractUsingEvent>(OnInteractUsing,
            before: new[] { typeof(WiresSystem), typeof(SharedDoorSystem), typeof(PoweredLightSystem) });
        SubscribeLocalEvent<BreakdownComponent, BreakdownRepairDoAfterEvent>(OnRepairDoAfter);
        SubscribeLocalEvent<BreakdownComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<BreakdownComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
        SubscribeLocalEvent<BreakdownComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<RepairNavigatorComponent, PlayerDetachedEvent>(OnNavigatorDetached);
        SubscribeLocalEvent<RepairNavigatorComponent, MobStateChangedEvent>(OnNavigatorMobState);
    }

    #region Public API

    public bool CanBreak(EntityUid uid, BreakdownPrototype proto)
    {
        if (TerminatingOrDeleted(uid) || HasComp<BreakdownComponent>(uid))
            return false;

        return _whitelist.IsValid(proto.Whitelist, uid) && !_whitelist.IsBlacklistPass(proto.Blacklist, uid);
    }

    public bool TryBreak(EntityUid uid, ProtoId<BreakdownPrototype> protoId, EntityUid? rule)
    {
        if (!_proto.TryIndex(protoId, out var proto) || !CanBreak(uid, proto))
            return false;

        var comp = AddComp<BreakdownComponent>(uid);
        comp.Breakdown = protoId;
        comp.Rule = rule;
        comp.Step = 0;
        comp.NextBurst = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(1f, Math.Max(1.1f, proto.BurstInterval)));

        ApplyEffects(uid, comp, proto);

        if (proto.AttachedEffect is { } attached)
            comp.AttachedEffect = SpawnAttachedTo(attached, new EntityCoordinates(uid, 0, 0));

        if (proto.BurstEffect is { } burst)
            Spawn(burst, _transform.GetMapCoordinates(uid));

        if (proto.BreakSound != null)
            _audio.PlayPvs(proto.BreakSound, uid);

        Dirty(uid, comp);
        _navigatorAccumulator = NavigatorUpdateInterval;
        return true;
    }

    public void ResolveBreakdown(EntityUid uid, BreakdownComponent? comp = null, EntityUid? user = null, bool repaired = true)
    {
        if (!Resolve(uid, ref comp, false))
            return;

        var reward = 0;
        if (repaired && _proto.TryIndex(comp.Breakdown, out var proto))
            reward = proto.Reward;

        RestoreEffects(uid, comp);

        if (repaired)
            _audio.PlayPvs(RepairedSound, uid);

        var rule = comp.Rule;
        comp.Rule = null;
        RemComp<BreakdownComponent>(uid);

        if (rule != null && !TerminatingOrDeleted(rule.Value))
        {
            var ev = new BreakdownResolvedEvent(uid, user, repaired, reward);
            RaiseLocalEvent(rule.Value, ref ev);
        }

        _navigatorAccumulator = NavigatorUpdateInterval;
    }

    public void Escalate(EntityUid uid, BreakdownComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false) || !_proto.TryIndex(comp.Breakdown, out var proto))
            return;

        if (proto.BurstEffect is { } burst)
            Spawn(burst, _transform.GetMapCoordinates(uid));

        if (proto.EscalationIntensity > 0)
            _explosion.QueueExplosion(uid, "Default", proto.EscalationIntensity, 3f, 6f, canCreateVacuum: false);

        ResolveBreakdown(uid, comp, repaired: false);
    }

    public string GetLocation(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid))
            return Loc.GetString("engineering-breakdown-location-unknown");

        return FormattedMessage.RemoveMarkupPermissive(_navMap.GetNearestBeaconString((uid, Transform(uid))));
    }

    public bool IsEngineer(EntityUid mob)
    {
        if (!_mind.TryGetMind(mob, out var mindId, out _) || !_job.MindTryGetJobId(mindId, out var job) || job == null)
            return false;

        return _proto.TryIndex(EngineeringDepartment, out var dept) && dept.Roles.Contains(job.Value);
    }

    public bool IsActiveEngineer(EntityUid mob)
    {
        if (TerminatingOrDeleted(mob) || HasComp<GhostComponent>(mob) || !HasComp<ActorComponent>(mob))
            return false;

        if (_mobState.IsIncapacitated(mob))
            return false;

        if (!_mind.TryGetMind(mob, out var mindId, out var mind) || mind.OwnedEntity != mob)
            return false;

        if (!_job.MindTryGetJobId(mindId, out var job) || job == null)
            return false;

        return _proto.TryIndex(EngineeringDepartment, out var dept) && dept.Roles.Contains(job.Value);
    }

    #endregion

    #region Effects

    private void ApplyEffects(EntityUid uid, BreakdownComponent comp, BreakdownPrototype proto)
    {
        foreach (var effect in proto.Effects)
        {
            switch (effect)
            {
                case BreakdownEffect.ApcBreakerOff:
                    if (TryComp<ApcComponent>(uid, out var apc) && apc.MainBreakerEnabled)
                    {
                        _apc.ApcToggleBreaker(uid, apc);
                        comp.RestoreBreaker = true;
                    }
                    break;
                case BreakdownEffect.LightBlink:
                    if (TryComp<PoweredLightComponent>(uid, out var light) && !light.IsBlinking)
                    {
                        _light.ToggleBlinkingLight(uid, light, true);
                        comp.RestoreBlink = true;
                    }
                    break;
                case BreakdownEffect.PowerDisabled:
                    if (TryComp<ApcPowerReceiverComponent>(uid, out var receiver) && !receiver.PowerDisabled)
                    {
                        _receiver.SetPowerDisabled(uid, true, receiver);
                        comp.RestorePowerDisabled = true;
                    }
                    break;
                case BreakdownEffect.BoltDoor:
                    if (TryComp<DoorBoltComponent>(uid, out var bolts) && !bolts.BoltsDown)
                    {
                        _door.SetBoltsDown((uid, bolts), true);
                        comp.RestoreBolts = true;
                    }
                    break;
                case BreakdownEffect.BatteryOffline:
                    if (TryComp<PowerNetworkBatteryComponent>(uid, out var battery) && battery.Enabled)
                    {
                        battery.Enabled = false;
                        comp.RestoreBattery = true;
                    }
                    break;
            }
        }
    }

    private void RestoreEffects(EntityUid uid, BreakdownComponent comp)
    {
        if (comp.RestoreBreaker && TryComp<ApcComponent>(uid, out var apc) && !apc.MainBreakerEnabled)
            _apc.ApcToggleBreaker(uid, apc);

        if (comp.RestoreBlink && TryComp<PoweredLightComponent>(uid, out var light) && light.IsBlinking)
            _light.ToggleBlinkingLight(uid, light, false);

        if (comp.RestorePowerDisabled && TryComp<ApcPowerReceiverComponent>(uid, out var receiver))
            _receiver.SetPowerDisabled(uid, false, receiver);

        if (comp.RestoreBolts && TryComp<DoorBoltComponent>(uid, out var bolts))
            _door.SetBoltsDown((uid, bolts), false);

        if (comp.RestoreBattery && TryComp<PowerNetworkBatteryComponent>(uid, out var battery))
            battery.Enabled = true;

        comp.RestoreBreaker = comp.RestoreBlink = comp.RestorePowerDisabled = comp.RestoreBolts = comp.RestoreBattery = false;

        if (comp.AttachedEffect is { } fx && !TerminatingOrDeleted(fx))
            QueueDel(fx);
        comp.AttachedEffect = null;
    }

    private void OnShutdown(Entity<BreakdownComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.AttachedEffect is { } fx && !TerminatingOrDeleted(fx))
            QueueDel(fx);
        ent.Comp.AttachedEffect = null;

        if (ent.Comp.Rule is { } rule && !TerminatingOrDeleted(rule))
        {
            ent.Comp.Rule = null;
            var ev = new BreakdownResolvedEvent(ent, null, false, 0);
            RaiseLocalEvent(rule, ref ev);
        }

        _navigatorAccumulator = NavigatorUpdateInterval;
    }

    #endregion

    #region Repair

    private void OnInteractUsing(Entity<BreakdownComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryStartRepair(ent, args.User, args.Used, false))
            args.Handled = true;
    }

    private void OnGetVerbs(Entity<BreakdownComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Using is not { } used)
            return;

        if (!_proto.TryIndex(ent.Comp.Breakdown, out var proto) || ent.Comp.Step >= proto.Steps.Count)
            return;

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("engineering-breakdown-verb-repair"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/settings.svg.192dpi.png")),
            Act = () => TryStartRepair(ent, user, used, true),
            Priority = 5,
        });
    }

    private bool TryStartRepair(Entity<BreakdownComponent> ent, EntityUid user, EntityUid used, bool explicitRequest)
    {
        if (TerminatingOrDeleted(ent) || HasComp<GhostComponent>(user))
            return false;

        if (!_proto.TryIndex(ent.Comp.Breakdown, out var proto) || ent.Comp.Step >= proto.Steps.Count)
            return false;

        var step = proto.Steps[ent.Comp.Step];

        if (!_tool.HasQuality(used, step.Quality.Id))
        {
            if (explicitRequest)
                _popup.PopupEntity(Loc.GetString("engineering-breakdown-wrong-tool", ("hint", Loc.GetString(step.Hint))), ent, user);
            return false;
        }

        var now = _timing.CurTime;

        if (ent.Comp.RepairUser is { } current
            && now < ent.Comp.RepairLockUntil
            && !TerminatingOrDeleted(current)
            && !HasComp<GhostComponent>(current))
        {
            _popup.PopupEntity(Loc.GetString(current == user
                ? "engineering-breakdown-busy-self"
                : "engineering-breakdown-already-repairing"), ent, user);
            return true;
        }

        if (proto.ShockChance > 0 && _random.Prob(proto.ShockChance)
            && _electrocution.TryDoElectrocution(user, ent, proto.ShockDamage, TimeSpan.FromSeconds(2), true))
        {
            if (proto.BurstEffect is { } burst)
                Spawn(burst, _transform.GetMapCoordinates(ent));
            _popup.PopupEntity(Loc.GetString("engineering-breakdown-shocked"), ent, user, PopupType.MediumCaution);
            return true;
        }

        if (!_tool.UseTool(used, user, ent, step.Delay, step.Quality.Id, new BreakdownRepairDoAfterEvent(), step.Fuel))
            return false;

        ent.Comp.RepairUser = user;
        ent.Comp.RepairStep = ent.Comp.Step;
        ent.Comp.RepairLockUntil = now + TimeSpan.FromSeconds(step.Delay * 2f + 2f);
        return true;
    }

    private void OnRepairDoAfter(Entity<BreakdownComponent> ent, ref BreakdownRepairDoAfterEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.RepairUser != args.User)
            return;

        var startedStep = ent.Comp.RepairStep;
        ent.Comp.RepairUser = null;
        ent.Comp.RepairStep = -1;
        ent.Comp.RepairLockUntil = TimeSpan.Zero;

        if (args.Cancelled)
            return;

        args.Handled = true;

        if (!_proto.TryIndex(ent.Comp.Breakdown, out var proto)
            || ent.Comp.Step >= proto.Steps.Count
            || startedStep != ent.Comp.Step)
            return;

        var step = proto.Steps[ent.Comp.Step];
        ent.Comp.Step++;

        if (ent.Comp.Step >= proto.Steps.Count)
        {
            _popup.PopupEntity(Loc.GetString("engineering-breakdown-repaired", ("device", ent.Owner)), ent, args.User, PopupType.Medium);
            ResolveBreakdown(ent, ent.Comp, args.User);
            return;
        }

        _popup.PopupEntity(Loc.GetString(step.Done), ent, args.User);
        _audio.PlayPvs(StepSound, ent);
        Dirty(ent);
    }

    private void OnExamined(Entity<BreakdownComponent> ent, ref ExaminedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.Breakdown, out var proto))
            return;

        args.PushMarkup(Loc.GetString("engineering-breakdown-examine",
            ("name", Loc.GetString(proto.Name)),
            ("desc", Loc.GetString(proto.Description)),
            ("color", proto.Color.ToHex())), 5);

        if (ent.Comp.Step < proto.Steps.Count)
        {
            args.PushMarkup(Loc.GetString("engineering-breakdown-examine-step",
                ("hint", Loc.GetString(proto.Steps[ent.Comp.Step].Hint))), 4);
        }
    }

    #endregion

    #region Navigator

    private void OnNavigatorDetached(Entity<RepairNavigatorComponent> ent, ref PlayerDetachedEvent args)
    {
        RemCompDeferred<RepairNavigatorComponent>(ent);
    }

    private void OnNavigatorMobState(Entity<RepairNavigatorComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            RemCompDeferred<RepairNavigatorComponent>(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        _markers.Clear();

        var query = EntityQueryEnumerator<BreakdownComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (!_proto.TryIndex(comp.Breakdown, out var proto))
                continue;

            if (now >= comp.NextBurst)
            {
                comp.NextBurst = now + TimeSpan.FromSeconds(proto.BurstInterval * _random.NextFloat(0.6f, 1.4f));
                if (proto.BurstEffect is { } burst)
                    Spawn(burst, _transform.GetMapCoordinates(uid, xform));
                if (proto.BurstSound != null)
                    _audio.PlayPvs(proto.BurstSound, uid);
            }

            var map = _transform.GetMapCoordinates(uid, xform);
            if (map.MapId != MapId.Nullspace)
                _markers.Add(new RepairMarker(GetNetEntity(uid), map.Position, map.MapId, proto.Color, proto.Severity));
        }

        _navigatorAccumulator += frameTime;
        if (_navigatorAccumulator < NavigatorUpdateInterval)
            return;

        _navigatorAccumulator = 0f;
        UpdateNavigators();
    }

    private void UpdateNavigators()
    {
        _navRecipients.Clear();

        if (_markers.Count > 0)
        {
            var actors = EntityQueryEnumerator<ActorComponent>();
            while (actors.MoveNext(out var mob, out _))
            {
                if (!IsActiveEngineer(mob))
                    continue;

                _navRecipients.Add(mob);

                var nav = EnsureComp<RepairNavigatorComponent>(mob);
                if (nav.Markers.SequenceEqual(_markers))
                    continue;

                nav.Markers = new List<RepairMarker>(_markers);
                Dirty(mob, nav);
            }
        }

        _navToRemove.Clear();
        var navs = EntityQueryEnumerator<RepairNavigatorComponent>();
        while (navs.MoveNext(out var uid, out _))
        {
            if (!_navRecipients.Contains(uid))
                _navToRemove.Add(uid);
        }

        foreach (var uid in _navToRemove)
            RemComp<RepairNavigatorComponent>(uid);
    }

    #endregion
}
