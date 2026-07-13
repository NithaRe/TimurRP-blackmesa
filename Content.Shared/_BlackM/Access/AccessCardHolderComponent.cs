using Robust.Shared.GameStates;

namespace Content.Shared._BlackM.Access;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AccessCardHolderComponent : Component
{
    [DataField]
    public int MaxBadges = 6;

    [DataField]
    public string BadgeContainerId = "badge_slots";

    [DataField, AutoNetworkedField]
    public string FullName = string.Empty;

    [DataField, AutoNetworkedField]
    public string JobTitle = string.Empty;
}
