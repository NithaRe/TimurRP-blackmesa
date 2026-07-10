using Content.Shared._BlackM.Passport;
using Robust.Client.UserInterface;

namespace Content.Client._BlackM.Passport.UI;

public sealed class PassportCheckerBoundUserInterface : BoundUserInterface
{
    private PassportCheckerWindow? _window;

    public PassportCheckerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<PassportCheckerWindow>();
        _window.OnFieldSelected += field => SendMessage(new PassportCheckerSelectFieldMessage(field));
        _window.OnAccusePressed += () => SendMessage(new PassportCheckerAccuseMessage());
        _window.OnCleanPressed  += () => SendMessage(new PassportCheckerConfirmCleanMessage());
        _window.OnEjectPressed  += () => SendMessage(new PassportCheckerEjectMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not PassportCheckerBoundUserInterfaceState cast)
            return;

        _window.UpdateState(cast);
    }
}
