using Robust.Client.Graphics;

namespace Content.Client._BlackM.ZenRift;

public sealed partial class ZenRiftNavigatorSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;

    private ZenRiftNavigatorOverlay? _navOverlay;

    public override void Initialize()
    {
        base.Initialize();
        _navOverlay = new ZenRiftNavigatorOverlay();
        _overlay.AddOverlay(_navOverlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_navOverlay != null)
            _overlay.RemoveOverlay(_navOverlay);
        _navOverlay = null;
    }
}
