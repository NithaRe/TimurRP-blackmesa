using Robust.Shared.Prototypes;

namespace Content.Shared._BlackM.RandomVariant;

[RegisterComponent]
public sealed partial class RandomVariantComponent : Component
{
    [DataField(required: true)]
    public List<EntProtoId> Variants = new();
}
