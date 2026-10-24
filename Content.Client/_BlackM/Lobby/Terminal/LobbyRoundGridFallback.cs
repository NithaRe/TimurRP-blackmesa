using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._BlackM.Lobby.Terminal;


public sealed class LobbyRoundGridFallback : Control
{
    private const int CellSize = 48;
    private static readonly Color Fill = new(0.018f, 0.014f, 0.009f);
    private static readonly Color MinorLine = new(0.62f, 0.34f, 0.08f, 0.035f);
    private static readonly Color MajorLine = new(0.82f, 0.48f, 0.12f, 0.065f);

    public LobbyRoundGridFallback()
    {
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var width = PixelWidth;
        var height = PixelHeight;
        if (width <= 0 || height <= 0)
            return;

        handle.DrawRect(new UIBox2(0, 0, width, height), Fill);

        for (var x = 0; x < width; x += CellSize)
        {
            var major = x % (CellSize * 5) == 0;
            handle.DrawRect(new UIBox2(x, 0, x + 1, height), major ? MajorLine : MinorLine);
        }

        for (var y = 0; y < height; y += CellSize)
        {
            var major = y % (CellSize * 5) == 0;
            handle.DrawRect(new UIBox2(0, y, width, y + 1), major ? MajorLine : MinorLine);
        }
    }
}
