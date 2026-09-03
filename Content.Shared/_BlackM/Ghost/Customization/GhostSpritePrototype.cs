using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._BlackM.Ghost.Customization;

[Prototype("ghostSprite")]
public sealed partial class GhostSpritePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Name { get; private set; } = string.Empty;

    [DataField]
    public bool Locked { get; private set; }

    [DataField(required: true)]
    public SpriteSpecifier Sprite { get; private set; } = default!;

    [DataField]
    public Vector2 Scale { get; private set; } = new(1, 1);

    [DataField]
    public Color SpriteColor { get; private set; } = Color.White;
}