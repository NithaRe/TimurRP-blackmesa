using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._BlackM.Lobby.Terminal;
/// та самая шедевро отрисовка сетки

public sealed class TerminalGridBackground : Control
{
    private static readonly Color Fill = new(0.008f, 0.03f, 0.016f);
    private static readonly Color Minor = new(0.2f, 1f, 0.4f, 0.035f);
    private static readonly Color Major = new(0.2f, 1f, 0.4f, 0.07f);

    public float FillAlpha { get; set; } = 1f;

    public int Cell { get; set; } = 40;

    public TerminalGridBackground()
    {
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var w = PixelWidth;
        var h = PixelHeight;
        if (w <= 0 || h <= 0)
            return;

        handle.DrawRect(new UIBox2(0, 0, w, h), Fill.WithAlpha(FillAlpha));

        var i = 0;
        for (var x = 0; x < w; x += Cell, i++)
            handle.DrawRect(new UIBox2(x, 0, x + 1, h), i % 5 == 0 ? Major : Minor);

        i = 0;
        for (var y = 0; y < h; y += Cell, i++)
            handle.DrawRect(new UIBox2(0, y, w, y + 1), i % 5 == 0 ? Major : Minor);
    }
}
