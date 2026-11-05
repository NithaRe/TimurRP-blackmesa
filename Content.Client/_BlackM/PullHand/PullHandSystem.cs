using Robust.Client.Graphics;

namespace Content.Client._BlackM.PullHand;

public sealed class PullHandSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new PullHandOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<PullHandOverlay>();
    }
}
