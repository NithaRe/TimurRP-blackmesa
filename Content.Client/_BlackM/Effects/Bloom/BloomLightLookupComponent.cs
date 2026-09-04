using Robust.Shared.ComponentTrees;
using Robust.Shared.Physics;
using Content.Shared._BlackM.Effects.Bloom;

namespace Content.Client._BlackM.Effects.Bloom;

[RegisterComponent]
public sealed partial class BloomLightLookupComponent : Component, IComponentTreeComponent<BloomLightMarkerComponent>
{
    public DynamicTree<ComponentTreeEntry<BloomLightMarkerComponent>> Tree { get; set; } = default!;
}
