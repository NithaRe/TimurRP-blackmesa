using Content.Shared._BlackM.Access;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;

namespace Content.Client._BlackM.Access;

public sealed class ClientAccessCardHolderSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AccessCardHolderComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<AccessCardHolderComponent, EntInsertedIntoContainerMessage>(OnContainerModified);
        SubscribeLocalEvent<AccessCardHolderComponent, EntRemovedFromContainerMessage>(OnContainerModified);
    }

    private void OnStartup(EntityUid uid, AccessCardHolderComponent component, ComponentStartup args)
    {
        UpdateSprite(uid, component);
    }

    private void OnContainerModified(EntityUid uid, AccessCardHolderComponent component, ContainerModifiedMessage args)
    {
        if (args.Container.ID != component.BadgeContainerId)
            return;

        UpdateSprite(uid, component);
    }

    private void UpdateSprite(EntityUid uid, AccessCardHolderComponent component)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (!_container.TryGetContainer(uid, component.BadgeContainerId, out var container))
            return;

        for (int i = 0; i < component.MaxBadges; i++)
        {
            var layerId = $"badge_layer_{i}";

            if (!sprite.LayerMapTryGet(layerId, out int layer))
            {
                layer = sprite.AddBlankLayer();
                sprite.LayerMapSet(layerId, layer);
            }

            if (i < container.ContainedEntities.Count)
            {
                var badge = container.ContainedEntities[i];

                if (TryComp<BadgeComponent>(badge, out var badgeComp) && !string.IsNullOrEmpty(badgeComp.CardSpriteState))
                {
                    sprite.LayerSetVisible(layer, true);
                    sprite.LayerSetState(layer, badgeComp.CardSpriteState);
                }
                else
                {
                    sprite.LayerSetVisible(layer, false);
                }
            }
            else
            {
                sprite.LayerSetVisible(layer, false);
            }
        }
    }
}
