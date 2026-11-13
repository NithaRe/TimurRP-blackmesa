using System.Numerics;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client._BlackM.Lobby.Terminal;

public sealed class TerminalPriorityMeter : Control
{
    private const float CellW = 62f;
    private const float CellH = 22f;
    private const float Gap = 2f;

    private static readonly Color[] LevelColors =
    {
        Color.FromHex("#9C6B55"),
        Color.FromHex("#C48327"),
        Color.FromHex("#E89B23"),
        Color.FromHex("#FFC857"),
    };

    private static readonly Color HoverColor = Color.FromHex("#FFB52E");
    private static readonly Color SelectedText = Color.FromHex("#FFD166");
    private static readonly Color IdleText = Color.FromHex("#D4A04D");

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
        var n = labels.Length;
        MinSize = new Vector2(n * CellW + Math.Max(0, n - 1) * Gap, n == 0 ? 0f : CellH);
    }

    public void Select(int index)
    {
        _selected = index;
    }

    private int ZoneAt(float x, float y)
    {
        if (_labels.Length == 0 || x < 0 || y < 0 || y > Math.Max(CellH, Size.Y))
            return -1;

        var index = (int) (x / (CellW + Gap));
        return index >= 0 && index < _labels.Length ? index : -1;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        int target;
        if (args.Function == EngineKeyFunctions.UIClick)
        {
            target = ZoneAt(args.RelativePosition.X, args.RelativePosition.Y);
            if (target < 0)
                return;
        }
        else if (args.Function == EngineKeyFunctions.UIRightClick)
        {
            if (_labels.Length == 0)
                return;
            target = 0;
        }
        else
        {
            return;
        }

        args.Handle();

        if (target == _selected)
            return;

        _selected = target;
        TerminalSounds.Play(TerminalSounds.Select, -6f, 0.04f);
        OnSelected?.Invoke(target);
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

        ToolTip = _labels[zone] + "\n" + Loc.GetString("blackm-priority-meter-hint");
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
            var rect = new UIBox2(x0, 0f, x0 + CellW * s, CellH * s);
            var level = LevelColors[Math.Min(i, LevelColors.Length - 1)];
            var selected = i == _selected;
            var hovered = i == _hover;
            var lit = i == 0 ? _selected == 0 : _selected > 0 && i <= _selected;

            handle.DrawRect(rect, lit ? level.WithAlpha(selected ? 0.32f : 0.16f) : StyleNano.TerminalBlack);
            handle.DrawRect(rect, selected ? level : hovered ? HoverColor : StyleNano.TerminalGreenDim, false);
            if (selected)
            {
                var inner = new UIBox2(rect.Left + s, rect.Top + s, rect.Right - s, rect.Bottom - s);
                handle.DrawRect(inner, level.WithAlpha(0.8f), false);
            }

            handle.DrawRect(new UIBox2(x0, 0f, x0 + CellW * s, 2 * s), level.WithAlpha(lit ? 1f : 0.3f));

            var label = FitLabel(handle, _labels[i], CellW * s - 6 * s, s);
            var dimensions = handle.GetDimensions(_font, label, s);
            var textX = x0 + (CellW * s - dimensions.X) * 0.5f;
            var textY = (CellH * s - dimensions.Y) * 0.5f + s;
            var textColor = selected
                ? SelectedText
                : lit
                    ? level
                    : IdleText.WithAlpha(hovered ? 1f : 0.75f);
            handle.DrawString(_font, new Vector2(textX, textY), label, s, textColor);
        }
    }

    private string FitLabel(DrawingHandleScreen handle, string label, float maxWidth, float scale)
    {
        if (handle.GetDimensions(_font, label, scale).X <= maxWidth)
            return label;

        for (var len = label.Length - 1; len > 1; len--)
        {
            var cut = label[..len] + ".";
            if (handle.GetDimensions(_font, cut, scale).X <= maxWidth)
                return cut;
        }

        return label[..1];
    }
}
