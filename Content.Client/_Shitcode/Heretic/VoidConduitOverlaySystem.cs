using Content.Client._Shitcode.Heretic.UI;
using Robust.Client.Graphics;

namespace Content.Client._Shitcode.Heretic;

public sealed partial class VoidConduitOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new VoidConduitOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<VoidConduitOverlay>();
    }
}
