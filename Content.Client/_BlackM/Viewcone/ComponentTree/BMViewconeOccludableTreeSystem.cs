// Originally authored by mirrorcult for EphemeralSpace (https://github.com/EphemeralSpace/ephemeral-space)
// Ported to this project with modifications under the same license.

using System.Numerics;
using Content.Shared._BlackM.Viewcone;
using Robust.Client.GameObjects;
using Robust.Shared.ComponentTrees;
using Robust.Shared.Physics;

namespace Content.Client._BlackM.Viewcone.ComponentTree;

/// <summary>
///     Handles gathering sprites to modify alpha in the viewcone overlays
/// </summary>
public sealed class BMViewconeOccludableTreeSystem : ComponentTreeSystem<BMViewconeOccludableTreeComponent, BMViewconeOccludableComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    protected override bool DoFrameUpdate => true;
    protected override bool DoTickUpdate => false;
    protected override bool Recursive => false;

    protected override Box2 ExtractAabb(in ComponentTreeEntry<BMViewconeOccludableComponent> entry, Vector2 pos, Angle rot)
    {
        return _sprite.CalculateBounds((entry.Uid, Comp<SpriteComponent>(entry.Uid)), pos, rot, default).CalcBoundingBox();
    }
}
