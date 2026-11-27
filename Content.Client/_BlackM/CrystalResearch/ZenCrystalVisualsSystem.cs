using System.Numerics;
using Content.Shared._BlackM.CrystalResearch;
using Robust.Client.GameObjects;

namespace Content.Client._BlackM.CrystalResearch;

public sealed class ZenCrystalVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprites = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ZenCrystalVisualsComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnState(Entity<ZenCrystalVisualsComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;
        _sprites.SetScale((ent.Owner, sprite), new Vector2(1f + 0.2f * ent.Comp.Growth));
        var brightness = ent.Comp.Researched ? 0.25f + ent.Comp.Energy * 0.75f : 1f;
        _sprites.SetColor((ent.Owner, sprite), new Color(brightness, brightness, brightness));
    }
}
