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
            var end = fade.StartTime + TimeSpan.FromSeconds(fade.Lifetime);
            var fadeStart = end - TimeSpan.FromSeconds(fade.FadeDuration);

            if (cur < fadeStart)
                continue;

            var t = (float) Math.Clamp((cur - fadeStart).TotalSeconds / fade.FadeDuration, 0, 1);

            _sprite.SetColor((uid, sprite), sprite.Color.WithAlpha(1f - t));
            _sprite.SetScale((uid, sprite), new Vector2(1f - (1f - fade.EndScale) * t));
        }
    }
}
