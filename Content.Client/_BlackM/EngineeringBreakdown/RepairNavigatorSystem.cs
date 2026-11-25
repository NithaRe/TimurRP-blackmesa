using Robust.Client.Graphics;

namespace Content.Client._BlackM.EngineeringBreakdown;

public sealed partial class RepairNavigatorSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;

    private RepairNavigatorOverlay? _navOverlay;

    public override void Initialize()
    {
        base.Initialize();
        _navOverlay = new RepairNavigatorOverlay();
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
