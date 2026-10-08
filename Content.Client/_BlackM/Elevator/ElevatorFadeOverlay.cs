using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.Elevator;

public sealed class ElevatorFadeOverlay : Overlay
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IResourceCache _cache = default!;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private readonly Font _font;
    private float _fromAlpha;
    private float _toAlpha;
    private float _duration;
    private TimeSpan _start;
    private string? _text;
    private float _alpha;

    public bool Finished => _toAlpha <= 0f && Progress() >= 1f;

    public ElevatorFadeOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = 1000;
        _font = new VectorFont(_cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 24);
    }

    public void Fade(bool toBlack, float duration, string? text)
    {
        _fromAlpha = _alpha;
        _toAlpha = toBlack ? 1f : 0f;
        _duration = Math.Max(0.01f, duration);
        _start = _timing.RealTime;
        if (text != null)
            _text = text;
    }

    private float Progress() => Math.Clamp((float) (_timing.RealTime - _start).TotalSeconds / _duration, 0f, 1f);

    protected override void Draw(in OverlayDrawArgs args)
    {
        _alpha = MathHelper.Lerp(_fromAlpha, _toAlpha, Progress());
        if (_alpha <= 0.001f)
            return;

        var h = args.ScreenHandle;
        h.DrawRect(args.ViewportBounds, Color.Black.WithAlpha(_alpha));

        if (string.IsNullOrEmpty(_text))
            return;

        var size = h.GetDimensions(_font, _text, 1f);
        var pos = new Vector2(
            args.ViewportBounds.Center.X - size.X / 2f,
            args.ViewportBounds.Center.Y - size.Y / 2f);
        h.DrawString(_font, pos, _text, Color.White.WithAlpha(_alpha));
    }
}
