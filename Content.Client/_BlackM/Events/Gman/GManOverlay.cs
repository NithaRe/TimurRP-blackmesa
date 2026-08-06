using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.IoC;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.Events.Gman;

public sealed class GManOverlay : Overlay
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

    private readonly float _duration;
    private readonly TimeSpan _startTime;

    private const float FadeIn = 0.5f;
    private const float FadeOut = 1.0f;

    public GManOverlay(float duration)
    {
        IoCManager.InjectDependencies(this);
        _duration = duration;
        _startTime = _timing.RealTime;
    }

    public bool IsFinished => (_timing.RealTime - _startTime).TotalSeconds >= _duration;

    protected override void Draw(in OverlayDrawArgs args)
    {
        var alpha = CalculateAlpha();
        if (alpha <= 0f)
            return;

        var box = args.WorldAABB.Enlarged(2f);
        args.WorldHandle.DrawRect(box, new Color(0f, 0f, 0f, alpha));
    }

    private float CalculateAlpha()
    {
        var elapsed = (float)(_timing.RealTime - _startTime).TotalSeconds;

        if (elapsed < FadeIn)
            return elapsed / FadeIn;

        if (elapsed < _duration - FadeOut)
            return 1f;

        if (elapsed < _duration)
            return (_duration - elapsed) / FadeOut;

        return 0f;
    }
}
