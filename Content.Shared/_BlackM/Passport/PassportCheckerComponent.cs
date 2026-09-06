using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._BlackM.Passport;

[RegisterComponent, NetworkedComponent]
public sealed partial class PassportCheckerComponent : Component
{
    [DataField]
    public string SlotId = "passportChecker-slot";

    [DataField]
    public EntProtoId PermitPrototype = "BadgePrintPermit";

    [DataField]
    public int MissedErrorFine = 500;

    public string? SelectedField;

    public string? ConfirmedErrorField;
    public PassportCheckerResult Result = PassportCheckerResult.None;
}
