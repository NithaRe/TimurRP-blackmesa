using System.Collections.Generic;
using Content.Shared._BlackM.PhraseWheel;
using Robust.Shared.Prototypes;

namespace Content.Server._BlackM.Shop;

[RegisterComponent]
public sealed partial class ShopPhraseWheelGrantComponent : Component
{
    [ViewVariables]
    public HashSet<ProtoId<PhraseWheelCategoryPrototype>> Added = new();

    [ViewVariables]
    public bool OwnsWheel;
}
