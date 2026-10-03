using Robust.Client.Graphics;

namespace Content.Client._BlackM.NameTag;

public sealed class NameTagSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new NameTagOverlay());
        _overlays.AddOverlay(new NameTagAuraOverlay());
        _overlays.AddOverlay(new NameTagAuraSparksOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<NameTagOverlay>();
        _overlays.RemoveOverlay<NameTagAuraOverlay>();
        _overlays.RemoveOverlay<NameTagAuraSparksOverlay>();
    }
}