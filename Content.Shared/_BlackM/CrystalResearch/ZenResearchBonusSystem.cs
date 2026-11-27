using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Damage.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Systems;

namespace Content.Shared._BlackM.CrystalResearch;

public sealed class ZenResearchBonusSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _movement = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ZenResearchCrewBonusComponent, RefreshMovementSpeedModifiersEvent>(OnRefresh);
        SubscribeLocalEvent<ZenResearchCrewBonusComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ZenResearchCrewBonusComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ZenResearchCrewBonusComponent, BeforeStaminaDamageEvent>(OnStaminaDamage);
        SubscribeLocalEvent<ZenResearchCrewBonusComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnStartup(Entity<ZenResearchCrewBonusComponent> ent, ref ComponentStartup args)
    {
        _movement.RefreshMovementSpeedModifiers(ent);
    }

    private void OnShutdown(Entity<ZenResearchCrewBonusComponent> ent, ref ComponentShutdown args)
    {
        _movement.RefreshMovementSpeedModifiers(ent);
    }

    private void OnStaminaDamage(Entity<ZenResearchCrewBonusComponent> ent, ref BeforeStaminaDamageEvent args)
    {
        if (ent.Comp.LifeStage < ComponentLifeStage.Stopping && args.Value > 0f)
            args.Value *= 1f - ent.Comp.StaminaResistance;
    }

    private void OnDamageModify(EntityUid uid, ZenResearchCrewBonusComponent component, DamageModifyEvent args)
    {
        if (component.LifeStage >= ComponentLifeStage.Stopping ||
            (component.ThermalResistance <= 0f && component.RadiationResistance <= 0f && component.PoisonResistance <= 0f))
            return;
        var damage = new DamageSpecifier(args.Damage);
        foreach (var (type, amount) in damage.DamageDict.ToArray())
        {
            if (amount <= 0)
                continue;
            var resistance = type switch
            {
                "Heat" or "Cold" => component.ThermalResistance,
                "Radiation" => component.RadiationResistance,
                "Poison" => component.PoisonResistance,
                _ => 0f,
            };
            damage.DamageDict[type] = amount * (1f - resistance);
        }
        args.Damage = damage;
    }

    private void OnRefresh(EntityUid uid,
        ZenResearchCrewBonusComponent component,
        RefreshMovementSpeedModifiersEvent args)
    {
        if (component.LifeStage >= ComponentLifeStage.Stopping)
            return;

        var multiplier = 1f + component.Speed;
        if (TryComp<PullerComponent>(uid, out var puller) && puller.Pulling != null)
            multiplier *= 1f + component.PullSpeed;

        args.ModifySpeed(multiplier, multiplier);
    }
}
