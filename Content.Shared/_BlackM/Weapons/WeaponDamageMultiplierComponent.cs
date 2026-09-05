namespace Content.Shared._BlackM.Weapons;

[RegisterComponent]
public sealed partial class WeaponDamageMultiplierComponent : Component
{
    [DataField]
    public float Multiplier = 1.2f;
}
