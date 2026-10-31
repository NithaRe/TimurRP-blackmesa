using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.Gib;

public sealed class GibFlashOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private float _intensity;
    private float _time;
    private const float Duration = 0.45f;

    public void Flash(float intensity)
    {
        _intensity = Math.Max(_intensity * (1f - _time / Duration), intensity);
        _time = 0f;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        if (_intensity <= 0f)
            return;

        _time += args.DeltaSeconds;
        if (_time >= Duration)
            _intensity = 0f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_intensity <= 0f)
            return;

        var t = Math.Clamp(_time / Duration, 0f, 1f);
        var alpha = _intensity * 0.28f * (1f - t) * (1f - t);
        var b = args.ViewportBounds;
        var box = new UIBox2(b.Left, b.Top, b.Right, b.Bottom);

        args.ScreenHandle.DrawRect(box, new Color(0.35f, 0f, 0f, alpha * 0.45f));

        var edge = MathF.Min(b.Width, b.Height) * 0.22f;
        var c = new Color(0.30f, 0f, 0f, alpha);
        args.ScreenHandle.DrawRect(new UIBox2(box.Left, box.Top, box.Right, box.Top + edge), c);
        args.ScreenHandle.DrawRect(new UIBox2(box.Left, box.Bottom - edge, box.Right, box.Bottom), c);
        args.ScreenHandle.DrawRect(new UIBox2(box.Left, box.Top, box.Left + edge, box.Bottom), c);
        args.ScreenHandle.DrawRect(new UIBox2(box.Right - edge, box.Top, box.Right, box.Bottom), c);
    }
}
