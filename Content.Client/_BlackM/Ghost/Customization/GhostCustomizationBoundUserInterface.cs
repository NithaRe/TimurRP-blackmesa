using Content.Shared._BlackM.Ghost.Customization;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._BlackM.Ghost.Customization;

[UsedImplicitly]
public sealed class GhostCustomizationBoundUserInterface : BoundUserInterface
{
    private GhostCustomizationWindow? _window;

    public GhostCustomizationBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<GhostCustomizationWindow>();
        _window.OnSpriteChosen += id => SendMessage(new GhostSpriteSelectedMessage(id));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is GhostCustomizationBoundUserInterfaceState s && _window != null)
            _window.UpdateState(s);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Dispose();
    }
}
