using Content.Shared._BlackM.Weapons;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Server._BlackM.Weapons;

public sealed class WeaponDamageMultiplierSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WeaponDamageMultiplierComponent, AmmoShotEvent>(OnAmmoShot);
        SubscribeLocalEvent<WeaponDamageMultiplierComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnAmmoShot(Entity<WeaponDamageMultiplierComponent> ent, ref AmmoShotEvent args)
    {
        foreach (var projectileUid in args.FiredProjectiles)
        {
            if (!TryComp<ProjectileComponent>(projectileUid, out var projectile))
                continue;

            projectile.Damage *= ent.Comp.Multiplier;
        }
    }

    private void OnMeleeHit(Entity<WeaponDamageMultiplierComponent> ent, ref MeleeHitEvent args)
    {
        args.BonusDamage += args.BaseDamage * (ent.Comp.Multiplier - 1f);
    }
}
