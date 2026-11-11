using Content.Shared.ActionBlocker;
using Content.Shared.Movement.Events;

namespace Content.Shared._BlackM.VoidFall;

public abstract class SharedBlackMVoidFallSystem : EntitySystem
{
    [Dependency] protected readonly ActionBlockerSystem Blocker = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlackMVoidFallingComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BlackMVoidFallingComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BlackMVoidFallingComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
    }

    private void OnStartup(Entity<BlackMVoidFallingComponent> ent, ref ComponentStartup args)
    {
        Blocker.UpdateCanMove(ent);
    }

    private void OnShutdown(Entity<BlackMVoidFallingComponent> ent, ref ComponentShutdown args)
    {
        Blocker.UpdateCanMove(ent);
    }

    private void OnUpdateCanMove(EntityUid uid, BlackMVoidFallingComponent component, UpdateCanMoveEvent args)
    {
        if (component.LifeStage <= ComponentLifeStage.Running)
            args.Cancel();
    }
}
