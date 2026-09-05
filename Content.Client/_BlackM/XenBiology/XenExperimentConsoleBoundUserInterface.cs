using Content.Shared._BlackM.XenBiology;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._BlackM.XenBiology;

[UsedImplicitly]
public sealed class XenExperimentConsoleBoundUserInterface : BoundUserInterface
{
    private XenExperimentConsoleWindow? _window;

    public XenExperimentConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<XenExperimentConsoleWindow>();
        _window.StartRequested += experimentId => SendMessage(new XenExperimentStartMessage(experimentId));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is XenExperimentConsoleBoundUserInterfaceState consoleState)
            _window?.UpdateState(consoleState);
    }
}
