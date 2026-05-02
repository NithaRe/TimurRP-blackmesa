using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Shared._BlackM.Houndeye;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Throwing;
using Content.Shared.Chat;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.Houndeye;

public sealed class HoundeyeSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    private static readonly SoundSpecifier ScreamSound =
        new SoundPathSpecifier("/Audio/_BlackM/houndeye/scream.ogg");

    private static readonly SoundSpecifier ChargeSound =
        new SoundPathSpecifier("/Audio/_BlackM/houndeye/charge.ogg");

    private readonly Dictionary<EntityUid, HoundeyeChargeEvent> _chargingData = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HoundeyeComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<HoundeyeComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<HoundeyeComponent, HoundeyeScreamEvent>(OnScream);
        SubscribeLocalEvent<HoundeyeComponent, HoundeyeChargeEvent>(OnCharge);
        SubscribeLocalEvent<HoundeyeComponent, StartCollideEvent>(OnCollide);

        SubscribeLocalEvent<HoundeyeSlowedComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnInit(EntityUid uid, HoundeyeComponent comp, ComponentInit args)
    {
        comp.ScreamActionUid = _actions.AddAction(uid, comp.ScreamAction);
        comp.ChargeActionUid = _actions.AddAction(uid, comp.ChargeAction);
    }

    private void OnShutdown(EntityUid uid, HoundeyeComponent comp, ComponentShutdown args)
    {
        _actions.RemoveAction(comp.ScreamActionUid);
        _actions.RemoveAction(comp.ChargeActionUid);
        _chargingData.Remove(uid);
    }

    private void OnScream(EntityUid uid, HoundeyeComponent comp, HoundeyeScreamEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _audio.PlayPvs(ScreamSound, uid);

        // scream chat
        _chat.TrySendInGameICMessage(uid, Loc.GetString("houndeye-scream"), InGameICChatType.Speak, hideChat: true);

        var selfPos = _transform.GetMapCoordinates(uid);

        foreach (var target in _lookup.GetEntitiesInRange(selfPos, comp.ScreamRange))
        {
            if (target == uid)
                continue;
            if (!HasComp<MovementSpeedModifierComponent>(target))
                continue;

            // damage
            var dmg = new DamageSpecifier();
            dmg.DamageDict["Blunt"] = comp.ScreamDamage;
            _damage.TryChangeDamage(target, dmg, origin: uid);

            // slow
            var slowed = EnsureComp<HoundeyeSlowedComponent>(target);
            slowed.SlowModifier = comp.SlowModifier;
            slowed.EndTime = _timing.CurTime + TimeSpan.FromSeconds(comp.SlowDuration);
            _speed.RefreshMovementSpeedModifiers(target);

            // Popup
            _popup.PopupEntity(Loc.GetString("houndeye-scream-slowed"), target, target);
        }
    }

    private void OnRefreshSpeed(EntityUid uid, HoundeyeSlowedComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(comp.SlowModifier, comp.SlowModifier);
    }

    private void OnCharge(EntityUid uid, HoundeyeComponent comp, HoundeyeChargeEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        comp.IsCharging = true;
        _chargingData[uid] = args;

        var targetPos = _transform.ToMapCoordinates(args.Target);
        var selfPos = _transform.GetMapCoordinates(uid);
        var vec = (targetPos.Position - selfPos.Position).Normalized() * args.Distance;

        _throwing.TryThrow(uid, vec, args.Speed, animated: false);
        _audio.PlayPvs(ChargeSound, uid);

        // scream
        _chat.TrySendInGameICMessage(uid, Loc.GetString("houndeye-charge"), InGameICChatType.Speak, hideChat: true);
    }

    private void OnCollide(EntityUid uid, HoundeyeComponent comp, ref StartCollideEvent args)
    {
        if (!comp.IsCharging)
            return;

        var target = args.OtherEntity;
        if (target == uid)
            return;
        if (!HasComp<MovementSpeedModifierComponent>(target))
            return;
        if (!_chargingData.TryGetValue(uid, out var chargeArgs))
            return;

        var selfPos = _transform.GetMapCoordinates(uid);
        var targetPos = _transform.GetMapCoordinates(target);
        var dir = (targetPos.Position - selfPos.Position).Normalized();

        _throwing.TryThrow(target, dir * chargeArgs.ThrowStrength, chargeArgs.ThrowStrength, uid);

        // Popup
        _popup.PopupEntity(Loc.GetString("houndeye-charge-hit"), target, target);

        if (TryComp<StaminaComponent>(target, out _))
            _stamina.TakeStaminaDamage(target, chargeArgs.StaminaDamage, source: uid);

        comp.IsCharging = false;
        _chargingData.Remove(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<HoundeyeSlowedComponent>();

        while (query.MoveNext(out var uid, out var slowed))
        {
            if (now < slowed.EndTime)
                continue;

            RemComp<HoundeyeSlowedComponent>(uid);
            _speed.RefreshMovementSpeedModifiers(uid);

            // Popup
            _popup.PopupEntity(Loc.GetString("houndeye-scream-slowed-end"), uid, uid);
        }
    }
}