using Content.Shared.Hands.Components;
using Content.Shared.Interaction.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Zombies;

#pragma warning disable IDE0130
namespace Content.Server.Zombies;

public sealed partial class ZombieSystem
{
    private void OnZombieMapInit(EntityUid uid, ZombieComponent component, MapInitEvent args)
    {
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
