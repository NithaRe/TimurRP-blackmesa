using System.Collections.Generic;
using Content.Shared._BlackM.Events.Gman;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.Events.Gman;

public sealed class GManVoidVisualsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private const float GrowTime = 0.6f;

    private sealed class VoidState
    {
        public TimeSpan FirstSeen;
        public TimeSpan? FadeStart;
        public float FadeTime;
    }

    private readonly Dictionary<EntityUid, VoidState> _tracked = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<GManDoorFadeEvent>(OnDoorFade);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _tracked.Clear();
    }

    private void OnDoorFade(GManDoorFadeEvent ev)
    {
        var uid = GetEntity(ev.Door);

        if (!_tracked.TryGetValue(uid, out var state))
        {
            state = new VoidState { FirstSeen = _timing.RealTime };
            _tracked[uid] = state;
        }

        state.FadeStart = _timing.RealTime;
        state.FadeTime = ev.FadeTime;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<GManVoidRectComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out var sprite))
        {
            if (!_tracked.TryGetValue(uid, out var state))
            {
                state = new VoidState { FirstSeen = _timing.RealTime };
                _tracked[uid] = state;
            }

            var growElapsed = (float)(_timing.RealTime - state.FirstSeen).TotalSeconds;
            var growAlpha = growElapsed >= GrowTime ? 1f : growElapsed / GrowTime;

            var fadeAlpha = 1f;
            if (state.FadeStart is { } fadeStart && state.FadeTime > 0f)
            {
                var fadeElapsed = (float)(_timing.RealTime - fadeStart).TotalSeconds;
                fadeAlpha = 1f - System.Math.Clamp(fadeElapsed / state.FadeTime, 0f, 1f);
            }

            sprite.Color = sprite.Color.WithAlpha(growAlpha * fadeAlpha);
        }

        if (_tracked.Count == 0)
            return;

        List<EntityUid>? toRemove = null;
        foreach (var uid in _tracked.Keys)
        {
            if (!Exists(uid))
                (toRemove ??= new List<EntityUid>()).Add(uid);
        }

        if (toRemove == null)
            return;

        foreach (var uid in toRemove)
            _tracked.Remove(uid);
    }
}
