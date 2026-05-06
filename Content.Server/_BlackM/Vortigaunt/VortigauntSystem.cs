using Content.Server.Chat.Systems;
using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Shared._BlackM.Vortigaunt;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Effects;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Content.Shared.Chat;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.Vortigaunt;

public sealed class VortigauntSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _colorFlash = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly SoundSpecifier LightningSound =
        new SoundPathSpecifier("/Audio/_BlackM/vortigaunt/lightning.ogg");

    private static readonly SoundSpecifier HealSound =
        new SoundPathSpecifier("/Audio/_BlackM/vortigaunt/heal.ogg");

    private static readonly SoundSpecifier StunWaveSound =
        new SoundPathSpecifier("/Audio/_BlackM/vortigaunt/stunwave.ogg");

    private const string LightningBeamProto = "VortigauntLightningBeamEffect";
    private const string StunWaveProto      = "VortigauntStunWaveEffect";
    private const string HealRingProto      = "VortigauntHealRingEffect";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VortigauntComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<VortigauntComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<VortigauntComponent, VortigauntLightningEvent>(OnLightning);
        SubscribeLocalEvent<VortigauntComponent, VortigauntHealEvent>(OnHeal);
        SubscribeLocalEvent<VortigauntComponent, VortigauntHealDoAfterEvent>(OnHealDoAfter);
        SubscribeLocalEvent<VortigauntComponent, VortigauntStunWaveEvent>(OnStunWave);
    }

    private void OnInit(EntityUid uid, VortigauntComponent comp, ComponentInit args)
    {
        comp.LightningActionUid = _actions.AddAction(uid, comp.LightningAction);
        comp.HealActionUid      = _actions.AddAction(uid, comp.HealAction);
        comp.StunWaveActionUid  = _actions.AddAction(uid, comp.StunWaveAction);
    }

    private void OnShutdown(EntityUid uid, VortigauntComponent comp, ComponentShutdown args)
    {
        _actions.RemoveAction(comp.LightningActionUid);
        _actions.RemoveAction(comp.HealActionUid);
        _actions.RemoveAction(comp.StunWaveActionUid);
    }

    private void OnLightning(EntityUid uid, VortigauntComponent comp, VortigauntLightningEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        _audio.PlayPvs(LightningSound, uid);
        _chat.TrySendInGameICMessage(uid, Loc.GetString("vortigaunt-lightning"), InGameICChatType.Speak, hideChat: true);

        var targetPos = _transform.ToMapCoordinates(args.Target);
        EntityUid? firstTarget = null;

        foreach (var ent in _lookup.GetEntitiesInRange(targetPos, 1.5f))
        {
            if (ent == uid)
                continue;
            if (!HasComp<MobStateComponent>(ent))
                continue;
            if (_mobState.IsDead(ent))
                continue;

            firstTarget = ent;
            break;
        }

        if (firstTarget == null)
            return;

        var hitTargets = new HashSet<EntityUid> { uid };
        var current = firstTarget.Value;
        var previous = uid;

        for (var i = 0; i < args.ChainCount; i++)
        {
            if (hitTargets.Contains(current))
                break;

            hitTargets.Add(current);

            SpawnLightningBeam(previous, current);

            StrikeLightning(uid, current, args.Damage);

            EntityUid? next = null;
            var currentPos = _transform.GetMapCoordinates(current);

            foreach (var nearby in _lookup.GetEntitiesInRange(currentPos, args.ChainRange))
            {
                if (hitTargets.Contains(nearby))
                    continue;
                if (!HasComp<MobStateComponent>(nearby))
                    continue;
                if (_mobState.IsDead(nearby))
                    continue;

                next = nearby;
                break;
            }

            if (next == null)
                break;

            previous = current;
            current = next.Value;
        }
    }

    private void StrikeLightning(EntityUid source, EntityUid target, float dmgAmount)
    {
        _colorFlash.RaiseEffect(Color.LimeGreen, new List<EntityUid> { target }, Filter.Pvs(target, entityManager: EntityManager));

        var dmg = new DamageSpecifier();
        dmg.DamageDict["Shock"] = dmgAmount;
        _damage.TryChangeDamage(target, dmg, origin: source);

        _popup.PopupEntity(Loc.GetString("vortigaunt-lightning-hit"), target, target);
    }

    private void SpawnLightningBeam(EntityUid source, EntityUid target)
    {
        var sourcePos = _transform.GetMapCoordinates(source);
        var targetPos = _transform.GetMapCoordinates(target);

        if (sourcePos.MapId != targetPos.MapId)
            return;

        var diff = targetPos.Position - sourcePos.Position;
        var angle = new Angle(diff);

        var beam = Spawn(LightningBeamProto, new MapCoordinates(sourcePos.Position, sourcePos.MapId));
        _transform.SetWorldRotation(beam, angle);

        if (diff.Length() > 2f)
        {
            var mid = sourcePos.Position + diff * 0.5f;
            var beam2 = Spawn(LightningBeamProto, new MapCoordinates(mid, sourcePos.MapId));
            _transform.SetWorldRotation(beam2, angle);
        }
    }

    private void OnHeal(EntityUid uid, VortigauntComponent comp, VortigauntHealEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        _popup.PopupEntity(Loc.GetString("vortigaunt-heal-start"), uid, uid);

        var coords = Transform(uid).Coordinates;
        Spawn(HealRingProto, coords);

        var doAfterArgs = new DoAfterArgs(EntityManager, uid, comp.HealChannelTime, new VortigauntHealDoAfterEvent(), uid)
        {
            BreakOnMove   = true,
            BreakOnDamage = true,
            NeedHand      = false,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnHealDoAfter(EntityUid uid, VortigauntComponent comp, VortigauntHealDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        _audio.PlayPvs(HealSound, uid);

        var heal = new DamageSpecifier();
        heal.DamageDict["Brute"] = -comp.HealAmount / 2;
        heal.DamageDict["Burn"]  = -comp.HealAmount / 2;
        _damage.TryChangeDamage(uid, heal);

        _colorFlash.RaiseEffect(Color.Lime, new List<EntityUid> { uid }, Filter.Pvs(uid, entityManager: EntityManager));

        _popup.PopupEntity(Loc.GetString("vortigaunt-heal-done"), uid, uid);
        _chat.TrySendInGameICMessage(uid, Loc.GetString("vortigaunt-heal-chat"), InGameICChatType.Speak, hideChat: true);
    }

    private void OnStunWave(EntityUid uid, VortigauntComponent comp, VortigauntStunWaveEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        _audio.PlayPvs(StunWaveSound, uid);
        _chat.TrySendInGameICMessage(uid, Loc.GetString("vortigaunt-stunwave"), InGameICChatType.Speak, hideChat: true);

        var coords = Transform(uid).Coordinates;
        Spawn(StunWaveProto, coords);

        var selfPos = _transform.GetMapCoordinates(uid);
        var affected = new List<EntityUid>();

        foreach (var target in _lookup.GetEntitiesInRange(selfPos, comp.StunWaveRange))
        {
            if (target == uid)
                continue;
            if (!HasComp<MobStateComponent>(target))
                continue;
            if (_mobState.IsDead(target))
                continue;

            var dmg = new DamageSpecifier();
            dmg.DamageDict["Blunt"] = comp.StunWaveDamage;
            _damage.TryChangeDamage(target, dmg, origin: uid);

            _stun.TryUpdateStunDuration(target, TimeSpan.FromSeconds(comp.StunDuration));

            _popup.PopupEntity(Loc.GetString("vortigaunt-stunwave-hit"), target, target);
            affected.Add(target);
        }

        if (affected.Count > 0)
        {
            affected.Add(uid);
            _colorFlash.RaiseEffect(Color.LimeGreen, affected, Filter.Pvs(uid, entityManager: EntityManager));
        }
    }
}