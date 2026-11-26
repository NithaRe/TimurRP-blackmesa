using Content.Shared._BlackM.Cargo.Delivery;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._BlackM.Cargo.Delivery;

[UsedImplicitly]
public sealed class CargoDeliveryTerminalBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private CargoDeliveryTerminalWindow? _window;

    public CargoDeliveryTerminalBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CargoDeliveryTerminalWindow>();
        _window.OnSend += point => SendMessage(new CargoDeliverySendMessage(point));
        _window.OnPrintCrate += () => SendMessage(new CargoDeliveryPrintCrateMessage());
        _window.OnRecall += drone => SendMessage(new CargoDeliveryRecallMessage(drone));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is CargoDeliveryTerminalState cast)
            _window?.UpdateState(cast);
    }
}
