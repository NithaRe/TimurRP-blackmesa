using Content.Shared._BlackM.Underground;
using Robust.Client.UserInterface;

namespace Content.Client._BlackM.Underground;

public sealed class UndergroundConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private UndergroundConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<UndergroundConsoleWindow>();
        _window.StartRequested += () => SendMessage(new UndergroundConsoleStartMessage());
        _window.MissionSelected += mission => SendMessage(new UndergroundMissionSelectMessage(mission));
        _window.ClaimRequested += () => SendMessage(new UndergroundMissionClaimMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is UndergroundConsoleState consoleState)
            _window?.UpdateState(consoleState);
    }
}
