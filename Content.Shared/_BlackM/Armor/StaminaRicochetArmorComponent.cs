using Content.Shared._Shitmed.Targeting;
using Content.Shared.Inventory;
using Content.Shared.Weapons.Reflect;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._BlackM.Armor;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StaminaRicochetArmorComponent : Component
{
    [DataField, AutoNetworkedField]
    public ReflectType Reflects = ReflectType.NonEnergy;

    [DataField]
    public SlotFlags SlotFlags = SlotFlags.OUTERCLOTHING;

    [DataField]
    public TargetBodyPart? ProtectedParts = TargetBodyPart.Chest | TargetBodyPart.Groin;

    [DataField, AutoNetworkedField]
    public float MaxRicochetChance = 0.85f;

    [DataField, AutoNetworkedField]
    public float MinRicochetChance = 0.05f;

    [DataField, AutoNetworkedField]
    public float StaminaExponent = 2f;

    [DataField, AutoNetworkedField]
    public bool CritAlwaysPenetrates = true;

    [DataField]
    public Angle Spread = Angle.FromDegrees(60);

    [DataField]
    public float StaminaDamagePerRicochet = 12f;

    [DataField]
    public bool ApplyStaminaResistance = false;

    [DataField]
    public SoundSpecifier? SoundOnRicochet = new SoundCollectionSpecifier("MetalBulletImpact");

    [DataField]
    public SoundSpecifier? SoundOnPenetrate;

    [DataField]
    public LocId RicochetPopup = "armor-ricochet";

    [DataField]
    public LocId PenetratePopup = "armor-penetrated";

    [DataField]
    public bool Examinable = true;
}
