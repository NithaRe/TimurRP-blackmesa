using Content.Shared._BlackM.Teleportation.Components;
using Robust.Client.GameObjects;

namespace Content.Client._BlackM.Teleportation;

public sealed class TeleportGatewayVisualizerSystem : VisualizerSystem<TeleportGatewayComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, TeleportGatewayComponent component,
        ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        AppearanceSystem.TryGetData<bool>(uid, TeleportGatewayVisuals.Linked, out var linked, args.Component);

        if (args.Sprite.LayerMapTryGet(TeleportGatewayVisualLayers.Frame, out var frameLayer))
            args.Sprite.LayerSetVisible(frameLayer, true);

        if (args.Sprite.LayerMapTryGet(TeleportGatewayVisualLayers.On, out var onLayer))
            args.Sprite.LayerSetVisible(onLayer, linked);

        if (args.Sprite.LayerMapTryGet(TeleportGatewayVisualLayers.Portal, out var portalLayer))
            args.Sprite.LayerSetVisible(portalLayer, linked);
    }
}