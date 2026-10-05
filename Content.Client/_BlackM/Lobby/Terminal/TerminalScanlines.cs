using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.Lobby.Terminal;

public sealed class TerminalScanlines : Control
{
    private static readonly Color Phosphor = new(0.2f, 1f, 0.4f);

    private float _time;

    public int LineStep { get; set; } = 4;

    public float LineAlpha { get; set; } = 0.07f;

    public float VignetteStrength { get; set; } = 0.015f;

    public bool RollingBar { get; set; } = false;

    public bool Flicker { get; set; } = false;

    public TerminalScanlines()
    {
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _time += args.DeltaSeconds;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var w = PixelWidth;
        var h = PixelHeight;
        if (w <= 0 || h <= 0)
            return;

        if (LineAlpha > 0f)
        {
            var line = new Color(0f, 0f, 0f, LineAlpha);
            for (var y = 0; y < h; y += LineStep)
                handle.DrawRect(new UIBox2(0, y, w, y + 1), line);
        }

        if (Flicker)
        {
            var f = 0.004f + 0.003f * MathF.Sin(_time * 29f) * MathF.Sin(_time * 4.1f);
            if (f > 0f)
                handle.DrawRect(new UIBox2(0, 0, w, h), Phosphor.WithAlpha(f));
        }

        if (RollingBar)
        {
            const int slices = 6;
            var barHeight = h * 0.015f;
            var top = ((_time * 0.06f) % 1.2f - 0.1f) * h;
            var slice = barHeight / slices;
            for (var i = 0; i < slices; i++)
            {
                var a = 0.02f * MathF.Sin(MathF.PI * (i + 0.5f) / slices);
                handle.DrawRect(new UIBox2(0, top + i * slice, w, top + (i + 1) * slice), Phosphor.WithAlpha(a));
            }
        }

        if (VignetteStrength > 0f)
        {
            const int layers = 8;
            const int thickness = 18;
            for (var i = 0; i < layers; i++)
            {
                var inset = i * thickness;
                var color = new Color(0f, 0f, 0f, (layers - i) * VignetteStrength);
                handle.DrawRect(new UIBox2(0, inset, w, inset + thickness), color);
                handle.DrawRect(new UIBox2(0, h - inset - thickness, w, h - inset), color);
                handle.DrawRect(new UIBox2(inset, 0, inset + thickness, h), color);
                handle.DrawRect(new UIBox2(w - inset - thickness, 0, w - inset, h), color);
            }
        }
    }
}
