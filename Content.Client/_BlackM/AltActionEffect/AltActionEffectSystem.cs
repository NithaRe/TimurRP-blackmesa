using Content.Shared._BlackM.AltActionEffect;
using Content.Shared.Ghost;
using Robust.Client.Graphics;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.AltActionEffect;

public sealed class AltActionEffectSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;

    private static readonly TimeSpan DedupeTime = TimeSpan.FromSeconds(0.6);

    public readonly List<ActiveAltEffect> Effects = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AltInteractedEvent>(OnLocalAltInteracted);
        SubscribeNetworkEvent<AltActionEffectEvent>(OnNetworkEffect);

        _overlays.AddOverlay(new AltActionEffectOverlay(this));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<AltActionEffectOverlay>();
        Effects.Clear();
    }

    private void OnLocalAltInteracted(AltInteractedEvent ev)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (HasComp<GhostComponent>(ev.User))
            return;

        Play(ev.Target);
    }

    private void OnNetworkEffect(AltActionEffectEvent ev)
    {
        if (!TryGetEntity(ev.Target, out var maybeTarget) || maybeTarget is not { } target)
            return;

        var now = _timing.RealTime;
        foreach (var effect in Effects)
        {
            if (effect.Target == target && now - effect.Start < DedupeTime)
                return;
        }

        Play(target);
    }

    public void Play(EntityUid target)
    {
        if (Deleted(target))
            return;

        Effects.RemoveAll(e => e.Target == target);
        Effects.Add(new ActiveAltEffect(target, _timing.RealTime));
    }
}

public record struct ActiveAltEffect(EntityUid Target, TimeSpan Start);
