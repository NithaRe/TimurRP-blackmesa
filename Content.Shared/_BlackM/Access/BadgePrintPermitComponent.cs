using Robust.Shared.GameStates;

namespace Content.Shared._BlackM.Access;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BadgePrintPermitComponent : Component
{
    [DataField, AutoNetworkedField]
    public string IssuedFor { get; set; } = string.Empty;
}
