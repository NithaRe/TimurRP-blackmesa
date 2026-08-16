using Robust.Shared.GameStates;

namespace Content.Shared._BlackM.Ghost.Customization;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class GhostCustomizationComponent : Component
{
    [DataField, AutoNetworkedField]
    public string? SelectedSprite;
}