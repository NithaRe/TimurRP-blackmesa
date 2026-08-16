#pragma warning disable CS0618

using Content.Shared._BlackM.Ghost.Customization;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._BlackM.Ghost.Customization;

public sealed class GhostCustomizationVisualsSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GhostCustomizationComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnState(EntityUid uid, GhostCustomizationComponent component, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite)
            || string.IsNullOrEmpty(component.SelectedSprite)
            || !_proto.TryIndex<GhostSpritePrototype>(component.SelectedSprite, out var proto))
        {
            return;
        }

        sprite.LayerSetSprite(0, proto.Sprite);
        sprite.LayerSetShader(0, "unshaded");
        sprite.LayerSetColor(0, proto.SpriteColor);
        sprite.LayerSetScale(0, proto.Scale);
    }
}
