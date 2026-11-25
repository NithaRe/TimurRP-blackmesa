using Content.Server.Stunnable;
using Content.Shared._CorvaxGoob.Weapons.Misc;
using Content.Shared.StatusEffect;

namespace Content.Server._CorvaxGoob.Weapons.Misc;

public sealed partial class GrapplingGunHunterSystem : SharedGrapplingGunHunterSystem
{
    [Dependency] private StunSystem _stun = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GrapplingHookedHunterComponent, GrapplingHookHunterStunEvent>(OnGrapplingHookHunterStun);
    }

    private void OnGrapplingHookHunterStun(EntityUid uid, GrapplingHookedHunterComponent component, GrapplingHookHunterStunEvent args)
    {
        if (args.Handled)
            return;

        _stun.TryAddParalyzeDuration(uid, args.Duration);
        args.Handled = true;
    }
}
