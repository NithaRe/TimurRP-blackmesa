using Content.Shared.IdentityManagement.Components;

namespace Content.Shared._BlackM.AnonymousIdentity;

public sealed class AnonymousIdentitySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnonymousIdentityComponent, SeeIdentityAttemptEvent>(OnSeeIdentityAttempt);
    }

    private void OnSeeIdentityAttempt(EntityUid uid, AnonymousIdentityComponent comp, SeeIdentityAttemptEvent args)
    {
        args.Cancel();
    }
}