// Originally authored by mirrorcult for EphemeralSpace (https://github.com/EphemeralSpace/ephemeral-space)
// Ported to this project with modifications under the same license.

using Content.Shared._BlackM.Viewcone;
using Robust.Shared.ComponentTrees;
using Robust.Shared.Physics;

namespace Content.Client._BlackM.Viewcone.ComponentTree;

[RegisterComponent]
public sealed partial class BMViewconeOccludableTreeComponent : Component, IComponentTreeComponent<BMViewconeOccludableComponent>
{
    public DynamicTree<ComponentTreeEntry<BMViewconeOccludableComponent>> Tree { get; set; }
}
