using System.Numerics;
using Content.Shared._BlackM.CityScanner;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.CityScanner;

public sealed class CityScannerVisualsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private const float Height = 0.12f;
    private const float Amplitude = 0.05f;
    private const float Speed = 2.4f;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var time = (float) _timing.RealTime.TotalSeconds;
        var query = EntityQueryEnumerator<CityScannerComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out var sprite))
        {
            var wave = MathF.Sin(time * Speed + uid.Id * 0.73f);
            var offset = new Vector2(MathF.Sin(time * 1.1f + uid.Id) * 0.02f, Height + wave * Amplitude);
            Entity<SpriteComponent?> ent = (uid, sprite);

            if (_sprite.LayerMapTryGet(ent, CityScannerLayers.Body, out var body, false))
                _sprite.LayerSetOffset(ent, body, offset);

            if (_sprite.LayerMapTryGet(ent, CityScannerLayers.Eye, out var eye, false))
                _sprite.LayerSetOffset(ent, eye, offset);

            if (_sprite.LayerMapTryGet(ent, CityScannerLayers.Shadow, out var shadow, false))
                _sprite.LayerSetScale(ent, shadow, new Vector2(1f - wave * 0.08f));
        }
    }
}
