using Robust.Shared.GameStates;

namespace Content.Shared._BlackM.Access;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AccessCardHolderComponent : Component
{
    [DataField, AutoNetworkedField]
    public int MaxBadges = 6;

    [DataField, AutoNetworkedField]
    public string BadgeContainerId = "badge_slots";
}