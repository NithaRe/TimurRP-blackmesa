using Content.Server.Electrocution;
using Content.Server.Emp;
using Content.Shared._BlackM.XenBiology;
using Content.Shared.StatusEffect;

namespace Content.Server._BlackM.XenBiology;

public sealed partial class XenExperimentElectricalDischargeSystem : EntitySystem
{
    [Dependency] private ElectrocutionSystem _electrocution = default!;
    [Dependency] private EmpSystem _emp = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenExperimentElectricalDischargeComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(
        Entity<XenExperimentElectricalDischargeComponent> discharge,
        ref MapInitEvent args)
    {
        var coordinates = _transform.GetMapCoordinates(discharge);
        foreach (var (target, statusEffects) in
                 _lookup.GetEntitiesInRange<StatusEffectsComponent>(coordinates, discharge.Comp.Radius))
        {
            _electrocution.TryDoElectrocution(
                target,
                discharge,
                discharge.Comp.ShockDamage,
                discharge.Comp.ShockTime,
                true,
                statusEffects: statusEffects);
        }

        _emp.EmpPulse(
            coordinates,
            discharge.Comp.Radius,
            discharge.Comp.EmpEnergyConsumption,
            discharge.Comp.EmpDisabledDuration);
    }
}
