using System.Numerics;
using Content.Shared._BlackM.ZenRift;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.ZenRift;

public sealed partial class ZenRiftVisualsSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ZenRiftVisualsComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var visuals, out var sprite))
        {
            var elapsed = (float) (now - visuals.PhaseStart).TotalSeconds;
            var duration = MathF.Max(0.01f, (float) visuals.PhaseDuration.TotalSeconds);
            var t = Math.Clamp(elapsed / duration, 0f, 1f);
            var time = (float) now.TotalSeconds;

            float scale;
            float alpha;
            float spin = visuals.SpinSpeed;

            switch (visuals.Phase)
            {
                case ZenRiftPhase.Opening:
                    scale = 0.15f + 0.65f * t + 0.04f * MathF.Sin(time * 9f);
                    alpha = 0.4f + 0.5f * t;
                    spin *= 0.5f;
                    break;
                case ZenRiftPhase.Active:
                    scale = 1f + 0.07f * MathF.Sin(time * 3f);
                    alpha = 1f;
                    break;
                default:
                    var inv = 1f - t;
                    scale = MathF.Max(0.01f, inv * inv);
                    alpha = inv;
                    spin *= 3f;
                    break;
            }

            scale *= visuals.MaxScale;
            _sprite.SetScale((uid, sprite), new Vector2(scale, scale));
            _sprite.SetRotation((uid, sprite), Angle.FromDegrees(time * spin % 360f));
            _sprite.SetColor((uid, sprite), sprite.Color.WithAlpha(alpha));
        }
    }
}
