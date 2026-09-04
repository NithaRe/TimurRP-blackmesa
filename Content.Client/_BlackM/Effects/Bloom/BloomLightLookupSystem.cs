using System.Numerics;
using Content.Shared._BlackM.Effects.Bloom;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.ComponentTrees;
using Robust.Shared.Physics;

namespace Content.Client._BlackM.Effects.Bloom;

public sealed class BloomLightLookupSystem : ComponentTreeSystem<BloomLightLookupComponent, BloomLightMarkerComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    protected override bool DoFrameUpdate => true;
    protected override bool DoTickUpdate => false;
    protected override bool Recursive => true;

    protected override Box2 ExtractAabb(
        in ComponentTreeEntry<BloomLightMarkerComponent> entry,
        Vector2 pos,
        Angle rot)
    {
        var texture = _sprite.Frame0(entry.Component.GlowMask);
        var size = new Vector2(texture.Width, texture.Height) / EyeManager.PixelsPerMeter;
        var radius = size.Length() / 2f + entry.Component.GlowOffset.Length();
        var extents = new Vector2(radius);
        return new Box2(pos - extents, pos + extents);
    }
}
