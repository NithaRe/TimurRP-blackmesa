using Content.Shared._BlackM.Ams;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

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
        _window.SetEntity(Owner);
        _window.OnLaunchPressed += () => SendMessage(new AmsLaunchButtonMessage());
    }
}
