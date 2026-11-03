using System.Numerics;
using Content.Shared._BlackM.Gib;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.Gib;

public sealed class GibFadeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void FrameUpdate(float frameTime)
    {
        var cur = _timing.CurTime;
        var query = EntityQueryEnumerator<GibFadeComponent, SpriteComponent>();

        while (query.MoveNext(out var uid, out var fade, out var sprite))
        {
            var sinceStart = (float) (cur - fade.StartTime).TotalSeconds;
            var scale = 1f;
            var alpha = 1f;

            if (fade.GrowTime > 0f && sinceStart < fade.GrowTime)
            {
                var g = Math.Clamp(sinceStart / fade.GrowTime, 0f, 1f);
                g = 1f - (1f - g) * (1f - g);
                scale = fade.StartScale + (1f - fade.StartScale) * g;
            }

            var end = fade.StartTime + TimeSpan.FromSeconds(fade.Lifetime);
            var fadeStart = end - TimeSpan.FromSeconds(fade.FadeDuration);

            if (cur >= fadeStart)
            {
                var t = (float) Math.Clamp((cur - fadeStart).TotalSeconds / fade.FadeDuration, 0, 1);
                alpha = 1f - t;
                scale *= 1f - (1f - fade.EndScale) * t;
            }

            _sprite.SetColor((uid, sprite), sprite.Color.WithAlpha(alpha));
            _sprite.SetScale((uid, sprite), new Vector2(scale));
        }
    }
}
