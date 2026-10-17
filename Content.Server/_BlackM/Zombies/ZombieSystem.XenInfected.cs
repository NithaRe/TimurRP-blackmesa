using Content.Shared.Hands.Components;
using Content.Server.Atmos.Components;
using Content.Server.Body.Components;
using Content.Server.Temperature.Components;
using Content.Shared.Body.Components;
using Content.Shared.Interaction.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.SSDIndicator;
using Content.Shared.Weapons.Melee;
using Content.Shared.Zombies;

#pragma warning disable IDE0130
namespace Content.Server.Zombies;

public sealed partial class ZombieSystem
{
    [Dependency] private readonly MobThresholdSystem _xenThresholds = default!;

    private void ReduceXenInfectedThresholds(EntityUid uid)
    {
        if (!TryComp<MobThresholdsComponent>(uid, out var thresholds))
            return;

        if (_xenThresholds.TryGetThresholdForState(uid, MobState.Critical, out var critical, thresholds))
            _xenThresholds.SetMobStateThreshold(uid, critical.Value * 0.85f, MobState.Critical, thresholds);

        if (_xenThresholds.TryGetThresholdForState(uid, MobState.Dead, out var dead, thresholds))
            _xenThresholds.SetMobStateThreshold(uid, dead.Value * 0.85f, MobState.Dead, thresholds);
    }

    private void OnZombieMapInit(EntityUid uid, ZombieComponent component, MapInitEvent args)
    {
        // Pre-spawned infected skip the physiology changes in ZombifyEntity.
        RemComp<RespiratorComponent>(uid);
        RemComp<BarotraumaComponent>(uid);
        RemComp<HungerComponent>(uid);
        RemComp<ThirstComponent>(uid);
        RemComp<SSDIndicatorComponent>(uid);

        if (TryComp<BloodstreamComponent>(uid, out var bloodstream))
            _bloodstream.SetBloodLossThreshold((uid, bloodstream), 0f);

        if (TryComp<TemperatureComponent>(uid, out var temperature))
            temperature.ColdDamage.ClampMax(0);

        if (TryComp<HandsComponent>(uid, out var hands))
        {
            _hands.RemoveHands(uid);
            RemComp(uid, hands);
        }

        RemComp<ComplexInteractionComponent>(uid);
        RemComp<PullerComponent>(uid);

        if (TryComp<MeleeWeaponComponent>(uid, out var melee))
        {
            melee.Damage = component.DamageOnBite;
            Dirty(uid, melee);
        }
    }
}
