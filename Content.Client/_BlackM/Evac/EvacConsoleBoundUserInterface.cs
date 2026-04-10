using Content.Client._BlackM.Evac.UI;
using Content.Shared._BlackM.Evac;
using Robust.Client.UserInterface;

namespace Content.Client._BlackM.Evac;

public sealed class EvacConsoleBoundUserInterface : BoundUserInterface
{
    private EvacConsoleMenu? _menu;

    public EvacConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<EvacConsoleMenu>();
        _menu.OnOpen += () => SendMessage(new EvacConsoleOpenMessage());
        _menu.OnClose += () => SendMessage(new EvacConsoleCloseMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not EvacConsoleBoundUserInterfaceState evacState)
            return;
        _menu?.UpdateState(evacState.State, evacState.TargetTime, evacState.PortalReady);
    }
}