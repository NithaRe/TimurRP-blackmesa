using Content.Shared._BlackM.HireTerminal;
using Robust.Client.UserInterface;

namespace Content.Client._BlackM.HireTerminal.UI;

public sealed class HireTerminalBoundUserInterface : BoundUserInterface
{
    private HireTerminalWindow? _window;

    public HireTerminalBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<HireTerminalWindow>();
        _window.OnSubmit  += (position, comment) => SendMessage(new HireTerminalSubmitMessage(position, comment));
        _window.OnClaim   += () => SendMessage(new HireTerminalClaimMessage());
        _window.OnEject   += () => SendMessage(new HireTerminalEjectMessage());
        _window.OnApprove += id => SendMessage(new HireTerminalApproveMessage(id));
        _window.OnReject  += id => SendMessage(new HireTerminalRejectMessage(id));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not HireTerminalBoundUserInterfaceState cast)
            return;

        _window.UpdateState(cast);
    }
}
