using System;
using Content.Shared._BlackM.Access;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._BlackM.Access;

[UsedImplicitly]
public sealed class BadgePrinterBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private BadgePrinterWindow? _window;

    public BadgePrinterBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<BadgePrinterWindow>();
        _window.Title = Loc.GetString("badge-printer-window-title");

        _window.OnPrintPressed += selected =>
        {
            SendMessage(new BadgePrinterPrintMessage(selected));
        };

        _window.OnEjectPressed += () =>
        {
            SendMessage(new BadgePrinterEjectCardMessage());
        };

        _window.OnEjectPassportPressed += () =>
        {
            SendMessage(new BadgePrinterEjectPassportMessage());
        };

        _window.OnReprintPassportPressed += () =>
        {
            SendMessage(new BadgePrinterReprintPassportMessage());
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not BadgePrinterBuiState buiState)
            return;

        _window?.UpdateState(buiState);
    }
}
