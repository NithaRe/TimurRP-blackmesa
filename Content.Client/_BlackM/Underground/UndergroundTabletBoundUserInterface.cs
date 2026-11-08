using Content.Shared._BlackM.Underground;
using Robust.Client.UserInterface;

namespace Content.Client._BlackM.Underground;

public sealed class UndergroundTabletBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private UndergroundTabletWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<UndergroundTabletWindow>();
        _window.ReturnRequested += () => SendMessage(new UndergroundTabletEarlyReturnMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is UndergroundConsoleState tabletState)
            _window?.UpdateState(tabletState);
    }
}
