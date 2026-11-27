using Content.Shared._BlackM.CrystalResearch;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._BlackM.CrystalResearch;

[UsedImplicitly]
public sealed class ZenCrystalChamberBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private ZenCrystalChamberWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ZenCrystalChamberWindow>();
        _window.Command += message => SendMessage(message);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is ZenCrystalChamberState chamber)
            _window?.UpdateState(chamber);
    }
}

[UsedImplicitly]
public sealed class ZenResearchConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private ZenResearchConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ZenResearchConsoleWindow>();
        _window.Purchase += (id, perk) => SendMessage(new ZenResearchPurchaseMessage(id, perk));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is ZenResearchConsoleState console)
            _window?.UpdateState(console);
    }
}
