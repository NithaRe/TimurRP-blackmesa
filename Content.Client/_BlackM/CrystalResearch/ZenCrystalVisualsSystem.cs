using Content.Shared._BlackM.CrystalResearch;
using Robust.Client.GameObjects;

namespace Content.Client._BlackM.CrystalResearch;

public sealed class ZenCrystalVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprites = default!;
    private float _elapsed;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ZenGrowthMediumComponent, AfterAutoHandleStateEvent>(OnMediumState);
        SubscribeLocalEvent<ZenCrystalCoolantComponent, AfterAutoHandleStateEvent>(OnCoolantState);
    }

    private void OnMediumState(Entity<ZenGrowthMediumComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprites.SetColor((ent.Owner, sprite),
                ent.Comp.Spent
                    ? Color.FromHex("#626965")
                    : Color.FromHex(ent.Comp.Medium == ZenGrowthMedium.Mineral ? "#94BFE6" : "#97CC7B"));
    }

    private void OnCoolantState(Entity<ZenCrystalCoolantComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprites.SetColor((ent.Owner, sprite),
                ent.Comp.Remaining <= 0f ? Color.FromHex("#626965") : Color.FromHex("#80C1D4"));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        _elapsed += frameTime;
        var query = EntityQueryEnumerator<ZenCrystalVisualsComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var crystal, out var sprite))
        {
            var warning = crystal.Instability >= 0.75f;
            var pulse = crystal.Active || warning
                ? 0.5f + 0.5f * MathF.Sin(_elapsed * (crystal.Overloaded ? 10f : 4f))
                : 0f;
            var brightness = crystal.Researched ? 0.25f + crystal.Energy * 0.75f : 1f;
            var state = crystal.Growth >= 0.99f ? "mature" : crystal.Growth >= 0.33f ? "growing" : "seed";
            if (sprite.LayerMapTryGet(ZenCrystalVisualLayers.Base, out var baseLayer))
                _sprites.LayerSetRsiState((uid, sprite), baseLayer, state);
            _sprites.SetColor((uid, sprite), new Color(brightness, brightness, brightness));
            if (sprite.LayerMapTryGet(ZenCrystalVisualLayers.Deposits, out var deposits))
                _sprites.LayerSetVisible((uid, sprite), deposits, crystal.Deposits);
            if (sprite.LayerMapTryGet(ZenCrystalVisualLayers.Cracks, out var cracks))
            {
                _sprites.LayerSetRsiState((uid, sprite), cracks, state + "-cracks");
                _sprites.LayerSetVisible((uid, sprite), cracks, crystal.Instability >= 0.65f && !crystal.Researched);
            }
            if (!sprite.LayerMapTryGet(ZenCrystalVisualLayers.Glow, out var glow))
                continue;
            _sprites.LayerSetRsiState((uid, sprite), glow, state);
            _sprites.LayerSetVisible((uid, sprite), glow, crystal.Active || warning);
            var color = crystal.Overloaded || crystal.Instability >= 0.75f ? Color.FromHex("#EC7866") :
                crystal.Reaction >= 1f ? Color.FromHex("#9DE3AC") :
                crystal.Reaction < 0f ? Color.FromHex("#D9B663") :
                crystal.Exposure switch
                {
                    ZenCrystalExposure.Heat => Color.FromHex("#E4A16B"),
                    ZenCrystalExposure.Cold => Color.FromHex("#85CDDD"),
                    ZenCrystalExposure.Electricity => Color.FromHex("#D5DFFF"),
                    _ => Color.FromHex("#A6D877"),
                };
            _sprites.LayerSetColor((uid, sprite), glow, color.WithAlpha(0.2f + pulse * 0.5f));
        }

        var emitters = EntityQueryEnumerator<ZenCrystalEmitterComponent, SpriteComponent>();
        while (emitters.MoveNext(out var uid, out var emitter, out var sprite))
        {
            if (sprite.LayerMapTryGet(ZenCrystalApparatusLayers.Active, out var active))
                _sprites.LayerSetVisible((uid, sprite), active, emitter.Active);
        }
    }
}
