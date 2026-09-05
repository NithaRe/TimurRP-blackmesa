namespace Content.Shared._BlackM.XenBiology;

[RegisterComponent]
public sealed partial class XenExperimentElectricalDischargeComponent : Component
{
    [DataField]
    public float Radius = 5f;

    [DataField]
    public int ShockDamage = 30;

    [DataField]
    public TimeSpan ShockTime = TimeSpan.FromSeconds(5);

    [DataField]
    public float EmpEnergyConsumption = 50000f;

    [DataField]
    public float EmpDisabledDuration = 20f;
}
