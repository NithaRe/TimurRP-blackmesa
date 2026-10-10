using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._BlackM.PhraseWheel;

[Prototype("phraseWheelCategory")]
public sealed partial class PhraseWheelCategoryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Name { get; private set; } = string.Empty;

    [DataField]
    public SpriteSpecifier? Icon { get; private set; }

    [DataField]
    public Color Color { get; private set; } = Color.MediumPurple;

    [DataField]
    public int Order { get; private set; } = 0;
}
