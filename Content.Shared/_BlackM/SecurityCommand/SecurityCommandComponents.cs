using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.ViewVariables;

namespace Content.Shared._BlackM.SecurityCommand;

[RegisterComponent, NetworkedComponent]
public sealed partial class SecurityCommanderComponent : Component
{
    [DataField]
    public EntProtoId Action = "ActionBMSecurityCommand";

    [ViewVariables]
    public EntityUid? ActionEntity;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class SecurityOrderReceiverComponent : Component
{
    [DataField]
    public EntProtoId MapAction = "ActionBMSecurityMap";

    [ViewVariables]
    public EntityUid? MapActionEntity;
}
