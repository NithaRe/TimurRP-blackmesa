using System.Numerics;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._BlackM.Cargo.UI;

/// <summary>Resolution-independent mesa emblem for the terminal heading.</summary>
public sealed class CargoTerminalLogo : Control
{
    private readonly Vector2[] _mesa = new Vector2[4];

    protected override void Draw(DrawingHandleScreen handle)
    {
        var size = MathF.Min(PixelSize.X, PixelSize.Y);
        var center = PixelSize / 2;
        var color = StyleNano.TerminalGreen;

        handle.DrawCircle(center, size * 0.46f, color);
        handle.DrawCircle(center, size * 0.39f, StyleNano.TerminalBlack);

        _mesa[0] = center + new Vector2(-0.34f, 0.29f) * size;
        _mesa[1] = center + new Vector2(-0.12f, -0.08f) * size;
        _mesa[2] = center + new Vector2(0.22f, -0.08f) * size;
        _mesa[3] = center + new Vector2(0.34f, 0.29f) * size;

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, _mesa, color);
    }
}