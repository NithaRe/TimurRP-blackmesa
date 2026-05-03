using Robust.Shared.GameStates;

namespace Content.Shared._BlackM.PhraseWheel;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class PhraseWheelComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<string> AllowedCategories { get; set; } = new();
}
