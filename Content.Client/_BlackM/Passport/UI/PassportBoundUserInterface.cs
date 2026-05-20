using Content.Shared._BlackM.Passport;
using Robust.Client.UserInterface;

namespace Content.Client._BlackM.Passport.UI;

public sealed class PassportBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private PassportWindow? _window;

    public PassportBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        base.Open();
        _window = this.CreateWindow<PassportWindow>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not PassportBoundUserInterfaceState cast)
            return;

        _window.UpdateState(cast, EntMan);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
}
