using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._BlackM.PhraseWheel;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class PhraseWheelComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<PhraseWheelCategoryPrototype>> AllowedCategories { get; set; } = new();

    public TimeSpan NextUse;
}
