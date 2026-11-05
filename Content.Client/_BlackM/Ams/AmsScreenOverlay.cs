using System;
using Robust.Client.Graphics;
using Robust.Shared.IoC;
using Robust.Shared.Timing;
using Robust.Shared.Enums;
using Robust.Shared.Maths;

namespace Content.Client._BlackM.Ams;

public sealed class AmsScreenOverlay : Overlay
{
    private readonly AmsEffectsSystem _fx;
    private readonly IGameTiming _timing;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private static readonly Color Green = Color.FromHex("#4dff3a");

    public AmsScreenOverlay(AmsEffectsSystem fx)
    {
        _fx = fx;
        _timing = IoCManager.Resolve<IGameTiming>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _fx.CascadeTint > 0.005f || _fx.WhiteFlash > 0.005f || _fx.GreenFlash > 0.005f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.ScreenHandle;
        var box = new UIBox2(args.ViewportBounds.Left, args.ViewportBounds.Top,
            args.ViewportBounds.Right, args.ViewportBounds.Bottom);

        if (_fx.CascadeTint > 0.005f)
        {
            var pulse = 0.85f + 0.15f * MathF.Sin((float) _timing.RealTime.TotalSeconds * 5f);
            handle.DrawRect(box, Green.WithAlpha(0.16f * _fx.CascadeTint * pulse));
        }

        if (_fx.GreenFlash > 0.005f)
            handle.DrawRect(box, Green.WithAlpha(0.45f * _fx.GreenFlash * _fx.GreenFlash));

        if (_fx.WhiteFlash > 0.005f)
            handle.DrawRect(box, Color.White.WithAlpha(0.95f * _fx.WhiteFlash * _fx.WhiteFlash));
    }
}
