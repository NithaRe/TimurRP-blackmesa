using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.Lobby.Terminal;


public sealed class LobbyRoundGridOverlay : Control
{
    private const int CellSize = 72;
    private const int MajorEvery = 4;
    private static readonly Color MinorLine = new(1f, 0.57f, 0.16f, 0.012f);
    private static readonly Color MajorLine = new(1f, 0.57f, 0.16f, 0.026f);

    private float _time;

    public LobbyRoundGridOverlay()
    {
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _time = (_time + args.DeltaSeconds * 0.6f) % CellSize;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var width = PixelWidth;
        var height = PixelHeight;
        if (width <= 0 || height <= 0)
            return;

        var offset = (int) _time;
        for (var x = -CellSize + offset; x < width; x += CellSize)
        {
            var index = Math.Abs((x - offset) / CellSize);
            handle.DrawRect(new UIBox2(x, 0, x + 1, height), index % MajorEvery == 0 ? MajorLine : MinorLine);
        }

        for (var y = -CellSize + offset; y < height; y += CellSize)
        {
            var index = Math.Abs((y - offset) / CellSize);
            handle.DrawRect(new UIBox2(0, y, width, y + 1), index % MajorEvery == 0 ? MajorLine : MinorLine);
        }
    }
}
