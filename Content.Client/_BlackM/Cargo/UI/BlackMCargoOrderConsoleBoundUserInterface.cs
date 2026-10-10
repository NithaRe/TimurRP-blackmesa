using Content.Shared.Cargo.BUI;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Events;
using Content.Shared.IdentityManagement;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;

namespace Content.Client._BlackM.Cargo.UI;

/// <summary>Terminal presentation of the existing cargo request and approval protocol.</summary>
public sealed class BlackMCargoOrderConsoleBoundUserInterface : BoundUserInterface
{
    private CargoTerminalWindow? _window;

    public BlackMCargoOrderConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CargoTerminalWindow>();
        var player = IoCManager.Resolve<IPlayerManager>().LocalEntity;
        _window.Initialize(Owner, EntMan.EntityExists(player) ? Identity.Name(player.Value, EntMan) : string.Empty);
        _window.OrderSubmitted += (product, amount, requester, reason) =>
            SendMessage(new CargoConsoleAddOrderMessage(requester, reason, product, amount));
        _window.OrderApproved += id => SendMessage(new CargoConsoleApproveOrderMessage(id));
        _window.OrderCanceled += id => SendMessage(new CargoConsoleRemoveOrderMessage(id));
        _window.AccountAction += (account, amount) => SendMessage(new CargoConsoleWithdrawFundsMessage(account, amount));
        _window.LimitToggled += () => SendMessage(new CargoConsoleToggleLimitMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is CargoConsoleInterfaceState cargoState)
            _window?.UpdateState(cargoState);
    }
}
