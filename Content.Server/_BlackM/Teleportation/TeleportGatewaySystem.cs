using System.Linq;
using Content.Shared._BlackM.Teleportation.Components;
using Content.Shared.Teleportation.Components;
using Content.Shared.Teleportation.Systems;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.Teleportation;

public sealed class TeleportGatewaySystem : EntitySystem
{
    [Dependency] private readonly LinkedEntitySystem _link = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<TeleportGatewayComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<TeleportGatewayComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<TeleportGatewayComponent, MapInitEvent>(OnMapInit);
    }

    private void OnStartup(EntityUid uid, TeleportGatewayComponent comp, ComponentStartup args)
    {
        // wait mapinit
    }

    private void OnMapInit(EntityUid uid, TeleportGatewayComponent comp, MapInitEvent args)
    {
        Timer.Spawn(500, () =>
        {
            if (TerminatingOrDeleted(uid))
                return;

            TryLink(uid, comp);
        });
    }

    private void TryLink(EntityUid uid, TeleportGatewayComponent comp)
    {
        if (comp.LinkedPortal != null)
            return;

        var query = EntityQueryEnumerator<TeleportGatewayComponent>();
        while (query.MoveNext(out var otherUid, out var otherComp))
        {
            if (otherUid == uid)
                continue;

            if (otherComp.PairKey != comp.PairKey)
                continue;

            if (otherComp.LinkedPortal != null)
                continue;

            comp.LinkedPortal = otherUid;
            otherComp.LinkedPortal = uid;

            Dirty(uid, comp);
            Dirty(otherUid, otherComp);

            _link.TryLink(uid, otherUid, false);

            _appearance.SetData(uid, TeleportGatewayVisuals.Linked, true);
            _appearance.SetData(otherUid, TeleportGatewayVisuals.Linked, true);

            return;
        }
    }

    private void OnShutdown(EntityUid uid, TeleportGatewayComponent comp, ComponentShutdown args)
    {
        if (comp.LinkedPortal == null)
            return;

        if (!TryComp<TeleportGatewayComponent>(comp.LinkedPortal, out var linkedComp))
            return;

        var linkedPortalUid = comp.LinkedPortal.Value;

        if (TryComp<LinkedEntityComponent>(uid, out var myLinked))
        {
            foreach (var ent in myLinked.LinkedEntities.ToArray())
            {
                _link.TryUnlink(uid, ent);
            }
        }

        linkedComp.LinkedPortal = null;
        Dirty(linkedPortalUid, linkedComp);

        _appearance.SetData(linkedPortalUid, TeleportGatewayVisuals.Linked, false);
    }
}