using Robust.Shared.GameStates;

namespace Content.Shared._BlackM.Passport;

[RegisterComponent, NetworkedComponent]
public sealed partial class PassportCheckerComponent : Component
{
    [DataField]
    public string SlotId = "passportChecker-slot";

    public string? SelectedField;

    public string? ConfirmedErrorField;
    public PassportCheckerResult Result = PassportCheckerResult.None;
}
