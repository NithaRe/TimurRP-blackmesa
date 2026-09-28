using Content.Shared.Movement.Events;

namespace Content.Shared._BlackM.Vortigaunt;

public sealed class SharedVortigauntSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VortigauntComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
    }

    private void OnUpdateCanMove(EntityUid uid, VortigauntComponent comp, UpdateCanMoveEvent args)
    {
        if (comp.IsHealing)
            args.Cancel();
    }
}
