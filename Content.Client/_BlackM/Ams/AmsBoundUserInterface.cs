using Content.Shared._BlackM.Ams;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;

namespace Content.Client._BlackM.Ams;

[UsedImplicitly]
public sealed class AmsBoundUserInterface : BoundUserInterface
{
    private AmsWindow? _window;

    public AmsBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<AmsWindow>();
        _window.OnLaunch += () => SendMessage(new AmsLaunchButtonMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is AmsBuiState s)
            _window?.UpdateState(s);
    }
}
