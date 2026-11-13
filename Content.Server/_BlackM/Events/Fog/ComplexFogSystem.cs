using System.Numerics;
using Content.Shared._BlackM.Events.Fog;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.Events.Fog;

public sealed class ComplexFogSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public void StartFog(EntityUid uid, float fadeIn, float density, Color color)
    {
        var now = _timing.CurTime;

        if (TryComp<ComplexFogComponent>(uid, out var fog))
        {
            var current = ComplexFogMath.GetStrength(fog, now);
            fog.StartTime = now - TimeSpan.FromSeconds(current * fadeIn);
        }
        else
        {
            fog = AddComp<ComplexFogComponent>(uid);
            fog.StartTime = now;
        }

        fog.EndTime = null;
        fog.FadeInTime = fadeIn;
        fog.Density = density;
        fog.Color = color;

        var angle = _random.NextFloat(0f, MathF.Tau);
        fog.WindDirection = new Vector2(MathF.Cos(angle), MathF.Sin(angle));

        Dirty(uid, fog);
    }

    public void StopFog(EntityUid uid, float fadeOut)
    {
        if (!TryComp<ComplexFogComponent>(uid, out var fog) || fog.EndTime != null)
            return;

        fog.EndTime = _timing.CurTime;
        fog.FadeOutTime = fadeOut;
        Dirty(uid, fog);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ComplexFogComponent>();
        while (query.MoveNext(out var uid, out var fog))
        {
            if (fog.EndTime is not { } end)
                continue;

            if (now >= end + TimeSpan.FromSeconds(fog.FadeOutTime + 1f))
                RemCompDeferred<ComplexFogComponent>(uid);
        }
    }
}
