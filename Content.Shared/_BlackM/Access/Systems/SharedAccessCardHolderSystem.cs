using Content.Shared._BlackM.Access;
using Content.Shared.Access;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared._BlackM.Access.Systems;

public abstract class SharedAccessCardHolderSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AccessCardHolderComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, AccessCardHolderComponent component, ComponentStartup args)
    {
        _container.EnsureContainer<Container>(uid, component.BadgeContainerId);
    }

    public HashSet<ProtoId<AccessLevelPrototype>> GetAccessTags(EntityUid uid, AccessCardHolderComponent? holder = null)
    {
        var tags = new HashSet<ProtoId<AccessLevelPrototype>>();

        if (!Resolve(uid, ref holder))
            return tags;

        if (!_container.TryGetContainer(uid, holder.BadgeContainerId, out var container))
            return tags;

        foreach (var entity in container.ContainedEntities)
        {
            if (!TryComp<BadgeComponent>(entity, out var badge))
                continue;

            foreach (var tag in badge.AccessTags)
                tags.Add(tag);
        }

        return tags;
    }

    public Container? GetBadgeContainer(EntityUid uid, AccessCardHolderComponent? holder = null)
    {
        if (!Resolve(uid, ref holder))
            return null;

        return _container.TryGetContainer(uid, holder.BadgeContainerId, out var container)
            ? container as Container
            : null;
    }

    public bool CanInsertBadge(EntityUid uid, AccessCardHolderComponent? holder = null)
    {
        if (!Resolve(uid, ref holder))
            return false;

        var c = GetBadgeContainer(uid, holder);
        return c != null && c.ContainedEntities.Count < holder.MaxBadges;
    }
}
