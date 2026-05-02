using Content.Shared._BlackM.Portal;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.Client._BlackM.Portal;

public sealed class EvacPortalVisualizerSystem : VisualizerSystem<EvacPortalComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, EvacPortalComponent comp, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var isActive = comp.Status == EvacPortalStatus.Active;
        var isSyncing = comp.Status == EvacPortalStatus.Synchronizing || comp.Status == EvacPortalStatus.Ready;

        if (args.Sprite.LayerMapTryGet(EvacPortalVisualLayers.Portal, out var portalLayer))
            args.Sprite.LayerSetVisible(portalLayer, isActive);

        if (args.Sprite.LayerMapTryGet(EvacPortalVisualLayers.Active, out var activeLayer))
            args.Sprite.LayerSetVisible(activeLayer, isActive || isSyncing);
    }
}