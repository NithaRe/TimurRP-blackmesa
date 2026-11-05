using Content.Shared._BlackM.AltActionEffect;
using Content.Shared.Ghost;
using Robust.Shared.Player;

namespace Content.Server._BlackM.AltActionEffect;

public sealed class AltActionEffectSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AltInteractedEvent>(OnAltInteracted);
    }

    private void OnAltInteracted(AltInteractedEvent ev)
    {
        if (Deleted(ev.Target) || Deleted(ev.User))
            return;

        if (HasComp<GhostComponent>(ev.User))
            return;

        RaiseNetworkEvent(new AltActionEffectEvent(GetNetEntity(ev.User), GetNetEntity(ev.Target)),
            Filter.Pvs(ev.Target, entityManager: EntityManager));
    }
}
