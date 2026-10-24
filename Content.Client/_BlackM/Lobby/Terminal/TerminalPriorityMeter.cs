using System.Numerics;
using Content.Client.Stylesheets;
using Robust.Client.ResourceManagement;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client._BlackM.Lobby.Terminal;

public sealed class TerminalPriorityMeter : Control
{
    private const float CellW = 66f;
    private const float CellH = 19f;
    private const float Gap = 3f;

    private static readonly Color[] LevelColors =
    {
        Color.FromHex("#A17A45"),
        Color.FromHex("#C48327"),
        Color.FromHex("#E89B23"),
        Color.FromHex("#FFC857"),
    };

    [Dependency] private readonly IResourceCache _cache = default!;
    private readonly Font _font;

    private string[] _labels = Array.Empty<string>();
    private int _selected = -1;
    private int _hover = -1;

    public int Selected => _selected;

    public event Action<int>? OnSelected;

    public TerminalPriorityMeter()
    {
        IoCManager.InjectDependencies(this);
        _font = TerminalFonts.Bold(_cache, 9);
        MouseFilter = MouseFilterMode.Stop;
        VerticalAlignment = VAlignment.Center;
    }

    public void SetLabels(string[] labels)
    {
        _labels = labels;
        var columns = Math.Min(labels.Length, 2);
        var rows = columns == 0 ? 0 : (labels.Length + columns - 1) / columns;
        MinSize = new Vector2(columns * CellW + Math.Max(0, columns - 1) * Gap,
            rows * CellH + Math.Max(0, rows - 1) * Gap);
    }

    public void Select(int index)
    {
        _selected = index;
    }

    private int ZoneAt(float x, float y)
    {
        var column = (int) (x / (CellW + Gap));
        var row = (int) (y / (CellH + Gap));
        var index = row * 2 + column;
        if (x < 0 || y < 0 || column < 0 || column > 1 || row < 0 || y - row * (CellH + Gap) > CellH ||
            x - column * (CellW + Gap) > CellW || index >= _labels.Length)
            return -1;
        return index;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        var zone = ZoneAt(args.RelativePosition.X, args.RelativePosition.Y);
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

        var zone = ZoneAt(args.RelativePosition.X, args.RelativePosition.Y);
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
            var x0 = i % 2 * (CellW + Gap) * s;
            var y0 = i / 2 * (CellH + Gap) * s;
            var rect = new UIBox2(x0, y0, x0 + CellW * s, y0 + CellH * s);
            var level = LevelColors[Math.Min(i, LevelColors.Length - 1)];
            var selected = i == _selected;
            var hovered = i == _hover;

            handle.DrawRect(rect, selected ? level.WithAlpha(0.2f) : StyleNano.TerminalBlack);
            handle.DrawRect(rect,
                selected ? level : hovered ? Color.FromHex("#FFB52E") : StyleNano.TerminalGreenDim, false);
            handle.DrawRect(new UIBox2(x0, y0, x0 + CellW * s, y0 + 2 * s), level.WithAlpha(selected ? 1f : 0.35f));

            var label = _labels[i];
            var dimensions = handle.GetDimensions(_font, label, s);
            var textX = x0 + (CellW * s - dimensions.X) * 0.5f;
            var textY = y0 + (CellH * s - dimensions.Y) * 0.5f;
            var textColor = selected ? Color.FromHex("#FFD166") : Color.FromHex("#D4A04D").WithAlpha(hovered ? 1f : 0.9f);
            handle.DrawString(_font, new Vector2(textX, textY), label, s, textColor);
        }
    }
}
