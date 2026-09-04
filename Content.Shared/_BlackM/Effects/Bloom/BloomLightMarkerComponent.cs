using System.Numerics;
using Robust.Shared.ComponentTrees;
using Robust.Shared.Physics;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Utility;

namespace Content.Shared._BlackM.Effects.Bloom;

[RegisterComponent]
public sealed partial class BloomLightMarkerComponent : Component, IComponentTreeEntry<BloomLightMarkerComponent>
{
    [DataField]
    public SpriteSpecifier GlowMask = new SpriteSpecifier.Rsi(
        new ResPath("_BlackM/Effects/Glow/64.rsi"),
        "light_point");

    [DataField]
    public Vector2 GlowOffset = new(0f, 0.45f);

    [DataField]
    public Color GlowTint = Color.White;

    public EntityUid? TreeUid { get; set; }
    public DynamicTree<ComponentTreeEntry<BloomLightMarkerComponent>>? Tree { get; set; }
    public bool AddToTree => true;
    public bool TreeUpdateQueued { get; set; }
}
