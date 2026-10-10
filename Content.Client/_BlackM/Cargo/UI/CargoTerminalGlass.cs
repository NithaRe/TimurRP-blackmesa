using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._BlackM.Cargo.UI;

/// <summary>Static, input-transparent CRT scanlines. The monitor texture supplies curved glass highlights.</summary>
public sealed class CargoTerminalGlass : Control
{
    public CargoTerminalGlass()
    {
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var line = StyleNano.TerminalBlack.WithAlpha(0.10f);
        for (var y = 0; y < PixelSize.Y; y += 3)
            handle.DrawRect(new UIBox2(0, y, PixelSize.X, y + 1), line);
    }
}
