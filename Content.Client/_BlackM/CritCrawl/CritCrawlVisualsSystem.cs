using System.Numerics;
using Content.Shared._BlackM.CritCrawl;
using Robust.Client.GameObjects;

namespace Content.Client._BlackM.CritCrawl;

public sealed class CritCrawlVisualsSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly CritCrawlSystem _crawl = default!;

    private sealed class CrawlVisualState
    {
        public Vector2 LastPosition;
        public Vector2 AppliedOffset;
        public float Phase;
        public float Moving; // 0..1
    }

    private readonly Dictionary<EntityUid, CrawlVisualState> _states = new();
    private readonly List<EntityUid> _toRemove = new();

    private const float PullsPerTile = 2.2f;

    private const float LurchAmplitude = 0.045f;
    private const float SwayAmplitude = 0.025f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CritCrawlComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(EntityUid uid, CritCrawlComponent comp, ComponentShutdown args)
    {
        if (!_states.Remove(uid, out var state))
            return;

        if (TryComp<SpriteComponent>(uid, out var sprite))
            _sprite.SetOffset((uid, sprite), sprite.Offset - state.AppliedOffset);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<CritCrawlComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var crawl, out var sprite, out var xform))
        {
            var crawling = _crawl.CanCrawl(uid, crawl);

            if (!_states.TryGetValue(uid, out var state))
            {
                if (!crawling)
                    continue;

                state = new CrawlVisualState { LastPosition = xform.LocalPosition };
                _states[uid] = state;
            }

            var pos = xform.LocalPosition;
            var moved = (pos - state.LastPosition).Length();
            state.LastPosition = pos;
            if (moved > 1f)
                moved = 0f;

            var target = crawling && moved > 0.0005f ? 1f : 0f;
            state.Moving += (target - state.Moving) * MathF.Min(1f, frameTime * (target > state.Moving ? 12f : 5f));

            if (crawling)
                state.Phase = (state.Phase + moved * PullsPerTile) % 1f;

            var p = state.Phase;
            var lurch = p < 0.3f ? MathF.Sin(p / 0.3f * MathF.PI * 0.5f) : MathF.Cos((p - 0.3f) / 0.7f * MathF.PI * 0.5f);
            var sway = MathF.Sin(p * MathF.Tau);

            var newOffset = new Vector2(sway * SwayAmplitude, lurch * LurchAmplitude) * state.Moving;

            if ((newOffset - state.AppliedOffset).LengthSquared() > 1e-8f)
            {
                _sprite.SetOffset((uid, sprite), sprite.Offset - state.AppliedOffset + newOffset);
                state.AppliedOffset = newOffset;
            }

            if (!crawling && state.Moving < 0.001f)
            {
                if (state.AppliedOffset != Vector2.Zero)
                    _sprite.SetOffset((uid, sprite), sprite.Offset - state.AppliedOffset);

                _states.Remove(uid);
            }
        }

        if (_states.Count == 0)
            return;

        _toRemove.Clear();
        foreach (var uid in _states.Keys)
        {
            if (!Exists(uid))
                _toRemove.Add(uid);
        }

        foreach (var uid in _toRemove)
        {
            _states.Remove(uid);
        }
    }
}
