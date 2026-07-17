using Content.Shared.Access;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._BlackM.Access;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BadgeComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<AccessLevelPrototype>> AccessTags = new();

    [DataField, AutoNetworkedField]
    public Color ExamineColor = Color.FromHex("#c8c8c8");

    [DataField, AutoNetworkedField]
    public string CardSpriteState = string.Empty;
}
