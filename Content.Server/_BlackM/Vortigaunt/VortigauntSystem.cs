using System.Numerics;
using Content.Server.Body.Systems;
using Content.Server.Chat.Systems;
using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Shared._BlackM.Vortigaunt;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Effects;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Physics;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;

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
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;

    private static readonly SoundSpecifier LightningSound =
        new SoundPathSpecifier("/Audio/_BlackM/vortigaunt/lightning.ogg");

    private static readonly SoundSpecifier HealSound =
        new SoundPathSpecifier("/Audio/_BlackM/vortigaunt/heal.ogg");

    private static readonly SoundSpecifier StunWaveSound =
        new SoundPathSpecifier("/Audio/_BlackM/vortigaunt/stunwave.ogg");

    private const string LightningBeamProto = "VortigauntLightningBeamEffect";
    private const string StunWaveProto = "VortigauntStunWaveEffect";
    private const string HealRingProto = "VortigauntHealRingEffect";
    private const int LightningBlockMask =
        (int) (CollisionGroup.Opaque | CollisionGroup.Impassable | CollisionGroup.GlassLayer);

    private const float ClickTargetRadius = 1.5f;

    private const int MaxBeamSegments = 12;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VortigauntComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<VortigauntComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<VortigauntComponent, MobStateChangedEvent>(OnMobStateChanged);

        SubscribeLocalEvent<VortigauntComponent, VortigauntLightningEvent>(OnLightning);
        SubscribeLocalEvent<VortigauntComponent, VortigauntHealEvent>(OnHeal);
        SubscribeLocalEvent<VortigauntComponent, VortigauntHealDoAfterEvent>(OnHealDoAfter);
        SubscribeLocalEvent<VortigauntComponent, VortigauntStunWaveEvent>(OnStunWave);
    }

    private void OnInit(EntityUid uid, VortigauntComponent comp, ComponentInit args)
    {
        comp.LightningActionUid = _actions.AddAction(uid, comp.LightningAction);
        comp.HealActionUid = _actions.AddAction(uid, comp.HealAction);
        comp.StunWaveActionUid = _actions.AddAction(uid, comp.StunWaveAction);

        RemComp<HungerComponent>(uid);
        RemComp<ThirstComponent>(uid);
    }

    private void OnShutdown(EntityUid uid, VortigauntComponent comp, ComponentShutdown args)
    {
        _actions.RemoveAction(comp.LightningActionUid);
        _actions.RemoveAction(comp.HealActionUid);
        _actions.RemoveAction(comp.StunWaveActionUid);
    }

    private void OnMobStateChanged(EntityUid uid, VortigauntComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        SetHealing(uid, comp, false);

        _audio.PlayPvs(comp.DeathSound, uid);
    }

    private void OnLightning(EntityUid uid, VortigauntComponent comp, VortigauntLightningEvent args)
    {
        if (args.Handled)
            return;

        var clickPos = _transform.ToMapCoordinates(args.Target);

        var firstTarget = FindClosestTarget(uid, clickPos, ClickTargetRadius, new HashSet<EntityUid> { uid }, uid);

        if (firstTarget == null)
            return;

        args.Handled = true;

        _audio.PlayPvs(LightningSound, uid);
        _chat.TrySendInGameICMessage(uid, Loc.GetString("vortigaunt-lightning"), InGameICChatType.Speak, hideChat: true);

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

            var currentPos = _transform.GetMapCoordinates(current);

            var next = FindClosestTarget(current, currentPos, args.ChainRange, hitTargets, current);

            if (next == null)
                break;

            previous = current;
            current = next.Value;
        }
    }

    private EntityUid? FindClosestTarget(
        EntityUid losFrom,
        MapCoordinates center,
        float range,
        HashSet<EntityUid> exclude,
        EntityUid ignoreSelf)
    {
        EntityUid? best = null;
        var bestDist = float.MaxValue;

        foreach (var ent in _lookup.GetEntitiesInRange(center, range))
        {
            if (ent == ignoreSelf || exclude.Contains(ent))
                continue;
            if (!HasComp<MobStateComponent>(ent))
                continue;
            if (_mobState.IsDead(ent))
                continue;

            var pos = _transform.GetMapCoordinates(ent);
            if (pos.MapId != center.MapId)
                continue;

            if (!HasLineOfSight(losFrom, ent))
                continue;

            var dist = (pos.Position - center.Position).LengthSquared();
            if (dist >= bestDist)
                continue;

            bestDist = dist;
            best = ent;
        }

        return best;
    }

    private bool HasLineOfSight(EntityUid from, EntityUid to)
    {
        var a = _transform.GetMapCoordinates(from);
        var b = _transform.GetMapCoordinates(to);

        if (a.MapId != b.MapId || a.MapId == MapId.Nullspace)
            return false;

        var diff = b.Position - a.Position;
        var dist = diff.Length();

        if (dist < 0.05f)
            return true;

        var ray = new CollisionRay(a.Position, diff / dist, LightningBlockMask);

        foreach (var hit in _physics.IntersectRay(a.MapId, ray, dist, from, false))
        {
            if (hit.HitEntity == to || hit.HitEntity == from)
                continue;

            return false;
        }

        return true;
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
        var dist = diff.Length();

        if (dist < 0.05f)
            return;

        var dir = diff / dist;

        var angle = diff.ToAngle();

        const float spriteLength = 2f;
        const float half = spriteLength / 2f;

        var segments = Math.Clamp((int) MathF.Ceiling(dist / spriteLength), 1, MaxBeamSegments);

        for (var i = 0; i < segments; i++)
        {
            var centerDist = MathF.Max(half, MathF.Min(half + spriteLength * i, dist - half));

            var pos = sourcePos.Position + dir * centerDist;
            var beam = Spawn(LightningBeamProto, new MapCoordinates(pos, sourcePos.MapId));
            _transform.SetWorldRotation(beam, angle);
        }
    }

    private void OnHeal(EntityUid uid, VortigauntComponent comp, VortigauntHealEvent args)
    {
        if (args.Handled)
            return;

        if (comp.IsHealing)
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, uid, comp.HealChannelTime, new VortigauntHealDoAfterEvent(), uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
        {
            Log.Error($"Vortigaunt {ToPrettyString(uid)}: не удалось запустить heal do-after. Есть ли DoAfterComponent на мобе?");
            return;
        }

        args.Handled = true;

        SetHealing(uid, comp, true);

        _popup.PopupEntity(Loc.GetString("vortigaunt-heal-start"), uid, uid);
        Spawn(HealRingProto, Transform(uid).Coordinates);
    }

    private void OnHealDoAfter(EntityUid uid, VortigauntComponent comp, VortigauntHealDoAfterEvent args)
    {
        SetHealing(uid, comp, false);

        if (args.Cancelled)
            return;

        args.Handled = true;

        _audio.PlayPvs(HealSound, uid);

        if (TryComp<DamageableComponent>(uid, out var damageable))
        {
            var heal = new DamageSpecifier(damageable.Damage);
            var total = heal.GetTotal().Float();

            if (total > 0f)
            {
                var factor = MathF.Min(1f, comp.HealAmount / total);
                heal *= -factor;
                _damage.TryChangeDamage(uid, heal, ignoreResistances: true);
            }
        }

        _bloodstream.TryModifyBleedAmount(uid, -100f);

        _colorFlash.RaiseEffect(Color.Lime, new List<EntityUid> { uid }, Filter.Pvs(uid, entityManager: EntityManager));

        _popup.PopupEntity(Loc.GetString("vortigaunt-heal-done"), uid, uid);
        _chat.TrySendInGameICMessage(uid, Loc.GetString("vortigaunt-heal-chat"), InGameICChatType.Speak, hideChat: true);
    }

    private void SetHealing(EntityUid uid, VortigauntComponent comp, bool value)
    {
        if (comp.IsHealing == value)
            return;

        comp.IsHealing = value;
        Dirty(uid, comp);
        _blocker.UpdateCanMove(uid);
    }

    private void OnStunWave(EntityUid uid, VortigauntComponent comp, VortigauntStunWaveEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        _audio.PlayPvs(StunWaveSound, uid);
        _chat.TrySendInGameICMessage(uid, Loc.GetString("vortigaunt-stunwave"), InGameICChatType.Speak, hideChat: true);

        Spawn(StunWaveProto, Transform(uid).Coordinates);

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

            if (!HasLineOfSight(uid, target))
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