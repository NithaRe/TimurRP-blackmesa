using System.Numerics;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client._BlackM.Lobby.Terminal;

public sealed class TerminalPriorityMeter : Control
{
    private const float CellW = 28f;
    private const float CellH = 24f;
    private const float Gap = 3f;

    private static readonly Color[] LevelColors =
    {
        new(1f, 0.32f, 0.32f),
        new(0.17f, 0.75f, 0.31f),
        new(0.4f, 1f, 0.54f),
        new(0.92f, 1f, 0.35f),
    };

    private string[] _labels = Array.Empty<string>();
    private int _selected = -1;
    private int _hover = -1;

    public int Selected => _selected;

    public event Action<int>? OnSelected;

    public TerminalPriorityMeter()
    {
        MouseFilter = MouseFilterMode.Stop;
        VerticalAlignment = VAlignment.Center;
    }

    public void SetLabels(string[] labels)
    {
        _labels = labels;
        MinSize = new Vector2(labels.Length * (CellW + Gap) - Gap, CellH);
    }

    public void Select(int index)
    {
        _selected = index;
    }

    private int ZoneAt(float x)
    {
        var step = CellW + Gap;
        var i = (int) (x / step);
        if (i < 0 || i >= _labels.Length || x - i * step > CellW)
            return -1;
        return i;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        var zone = ZoneAt(args.RelativePosition.X);
        if (zone < 0)
            return;

        args.Handle();

        if (zone == _selected)
            return;

        _selected = zone;
        TerminalSounds.Play(TerminalSounds.Select, -6f, 0.04f);
        OnSelected?.Invoke(zone);
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        var zone = ZoneAt(args.RelativePosition.X);
        if (zone == _hover)
            return;

        _hover = zone;
        if (zone < 0)
            return;

        ToolTip = _labels[zone];
        TerminalSounds.Play(TerminalSounds.Hover, -12f, 0.06f, 40);
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        _hover = -1;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var s = UIScale;

        for (var i = 0; i < _labels.Length; i++)
        {
            var x0 = i * (CellW + Gap) * s;
            var rect = new UIBox2(x0, 0, x0 + CellW * s, CellH * s);
            var level = LevelColors[Math.Min(i, LevelColors.Length - 1)];
            var selected = i == _selected;
            var hovered = i == _hover;

            handle.DrawRect(rect, selected ? level.WithAlpha(0.92f) : StyleNano.TerminalBlack);
            handle.DrawRect(rect,
                selected ? level : hovered ? StyleNano.TerminalGreen : StyleNano.TerminalGreenDim, false);

            var glyph = selected ? new Color(0.02f, 0.08f, 0.04f) : level.WithAlpha(hovered ? 1f : 0.6f);

            if (i == 0)
            {
                // X
                var a = new Vector2(x0 + 8 * s, 7 * s);
                var b = new Vector2(x0 + (CellW - 8) * s, (CellH - 7) * s);
                handle.DrawLine(a, b, glyph);
                handle.DrawLine(new Vector2(a.X, b.Y), new Vector2(b.X, a.Y), glyph);
                continue;
            }

            // i bars of growing height, centred in the cell
            const float barW = 3f;
            const float barGap = 2f;
            var total = i * barW + (i - 1) * barGap;
            var startX = x0 + (CellW * s - total * s) / 2f;
            for (var k = 0; k < i; k++)
            {
                var height = (5f + 4f * k) * s;
                var bx = startX + k * (barW + barGap) * s;
                var bottom = (CellH - 5f) * s;
                handle.DrawRect(new UIBox2(bx, bottom - height, bx + barW * s, bottom), glyph);
            }
        }
    }
}
