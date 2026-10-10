using Content.Shared._BlackM.Elevator;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Client._BlackM.Elevator;

public sealed class ElevatorFadeSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;

    private ElevatorFadeOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<BlackMElevatorFadeEvent>(OnFade);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        RemoveOverlay();
    }

    private void OnFade(BlackMElevatorFadeEvent ev)
    {
        if (_overlay == null)
        {
            _overlay = new ElevatorFadeOverlay();
            _overlays.AddOverlay(_overlay);
        }

        _overlay.Fade(ev.ToBlack, ev.Duration, ev.Text);
    }

    public override void FrameUpdate(float frameTime)
    {
        if (_overlay is { Finished: true })
            RemoveOverlay();
    }

    private void RemoveOverlay()
    {
        if (_overlay == null)
            return;

        _overlays.RemoveOverlay(_overlay);
        _overlay = null;
    }
}
