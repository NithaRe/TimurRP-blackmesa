using Robust.Client.Graphics;
using Robust.Shared.GameObjects;

namespace Content.Client._BlackM.Overlays;

/// <summary>
/// global shader
/// </summary>
public sealed class DefaultAtmosphereSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay.AddOverlay(new DefaultOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<DefaultOverlay>();
    }
}