using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.ActionBlocker;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._BlackM.CritCrawl;

[ByRefEvent]
public record struct CritCrawlAttemptEvent(bool Allowed = false);

public sealed class CritCrawlSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CritCrawlComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<CritCrawlComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<CritCrawlComponent, CritCrawlAttemptEvent>(OnCrawlAttempt);
        SubscribeLocalEvent<CritCrawlComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnMobStateChanged(EntityUid uid, CritCrawlComponent comp, MobStateChangedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        comp.WoundCrit = args.NewMobState == MobState.Critical && IsWoundDamage(uid, comp);
        comp.TrailAccumulator = 0f;
        Dirty(uid, comp);

        Refresh(uid, comp, announce: args.NewMobState == MobState.Critical);
    }

    private void OnDamageChanged(EntityUid uid, CritCrawlComponent comp, DamageChangedEvent args)
    {
        if (!_mobState.IsCritical(uid))
            return;

        Refresh(uid, comp, announce: true);
    }

    private void OnCrawlAttempt(EntityUid uid, CritCrawlComponent comp, ref CritCrawlAttemptEvent args)
    {
        if (CanCrawl(uid, comp))
            args.Allowed = true;
    }

    private void OnRefreshSpeed(EntityUid uid, CritCrawlComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        if (CanCrawl(uid, comp))
            args.ModifySpeed(comp.SpeedModifier, comp.SpeedModifier);
    }

    private void Refresh(EntityUid uid, CritCrawlComponent comp, bool announce)
    {
        var canCrawl = CanCrawl(uid, comp);

        _blocker.UpdateCanMove(uid);
        _speed.RefreshMovementSpeedModifiers(uid);

        if (canCrawl == comp.LastCanCrawl)
            return;

        var wasCrawling = comp.LastCanCrawl;
        comp.LastCanCrawl = canCrawl;

        if (!announce || !_net.IsServer)
            return;

        if (canCrawl)
            _popup.PopupEntity(Loc.GetString("crit-crawl-can-crawl"), uid, uid, PopupType.MediumCaution);
        else if (wasCrawling && _mobState.IsCritical(uid))
            _popup.PopupEntity(Loc.GetString("crit-crawl-too-weak"), uid, uid, PopupType.LargeCaution);
    }

    public bool CanCrawl(EntityUid uid, CritCrawlComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false) || !comp.WoundCrit || !_mobState.IsCritical(uid))
            return false;

        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return true;

        if (!_thresholds.TryGetThresholdForState(uid, MobState.Critical, out var crit)
            || !_thresholds.TryGetThresholdForState(uid, MobState.Dead, out var dead)
            || dead.Value <= crit.Value)
            return true;

        var vital = _thresholds.CheckVitalDamage(uid, damageable);
        var progress = (vital - crit.Value).Float() / (dead.Value - crit.Value).Float();
        return progress <= comp.MaxCritProgress;
    }

    private bool IsWoundDamage(EntityUid uid, CritCrawlComponent comp)
    {
        if (!TryComp<DamageableComponent>(uid, out var damageable) || damageable.TotalDamage <= FixedPoint2.Zero)
            return false;

        var wounds = FixedPoint2.Zero;
        foreach (var group in comp.WoundGroups)
        {
            if (damageable.DamagePerGroup.TryGetValue(group, out var value))
                wounds += value;
        }

        return wounds.Float() / damageable.TotalDamage.Float() >= comp.MinWoundFraction;
    }
}
