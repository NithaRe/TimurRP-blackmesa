// Originally authored by mirrorcult for EphemeralSpace (https://github.com/EphemeralSpace/ephemeral-space)
// Ported to this project with modifications under the same license.

using Robust.Shared.ComponentTrees;
using Robust.Shared.GameStates;
using Robust.Shared.Physics;

namespace Content.Shared._BlackM.Viewcone;

/// <summary>
///     Marks an entity as one which should fade away clientside if you have a viewcone and it's out of view
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BMViewconeOccludableComponent : Component, IComponentTreeEntry<BMViewconeOccludableComponent>
{
    [DataField, AutoNetworkedField]
    public bool OccludeIfAnchored = false;

    /// <summary>
    ///     Whether the occluding should be inverted,
    ///     i.e. the sprite will be invisible while within view, and visible outside of view
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Inverted = false;

    /// <summary>
    ///     If this is a temporary entity (like an effect), then this is the originating player (or other source)
    ///     of this occludable.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Source = null;

    // Clientside comptree stuff
    public EntityUid? TreeUid { get; set; }
    public DynamicTree<ComponentTreeEntry<BMViewconeOccludableComponent>>? Tree { get; set; }
    public bool AddToTree => true;
    public bool TreeUpdateQueued { get; set; }
}
