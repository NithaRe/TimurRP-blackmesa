using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client.UserInterface.Systems.PhraseWheel;

public sealed class PhraseWheelColorBar : PanelContainer
{
    public event Action<Color?>? ColorChanged;

    private readonly LineEdit _hexInput;
    private readonly StyleBoxFlat _previewStyle;
    private readonly PanelContainer _preview;

    public PhraseWheelColorBar(Color? initial)
    {
        HorizontalAlignment = HAlignment.Center;
        MouseFilter = MouseFilterMode.Stop;
        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = new Color(0.07f, 0.07f, 0.09f, 0.88f),
            ContentMarginLeftOverride = 8,
            ContentMarginRightOverride = 8,
            ContentMarginTopOverride = 4,
            ContentMarginBottomOverride = 4,
        };

        _hexInput = new LineEdit
        {
            PlaceHolder = "#RRGGBB",
            MinSize = new Vector2(110, 26),
            Text = initial is { } c ? ToHex(c) : string.Empty,
        };
        _hexInput.OnTextChanged += args => OnHexChanged(args.Text);

        _previewStyle = new StyleBoxFlat { BackgroundColor = initial ?? Color.Transparent };
        _preview = new PanelContainer
        {
            MinSize = new Vector2(26, 26),
            PanelOverride = _previewStyle,
        };

        var reset = new Button { Text = "✕", MinSize = new Vector2(28, 26), ToolTip = Loc.GetString("phrase-wheel-color-reset") };
        reset.OnPressed += _ =>
        {
            _hexInput.Text = string.Empty;
            SetColor(null);
        };

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            VerticalAlignment = VAlignment.Center,
        };
        row.AddChild(new Label { Text = Loc.GetString("phrase-wheel-color-label"), VerticalAlignment = VAlignment.Center });
        row.AddChild(_hexInput);
        row.AddChild(_preview);
        row.AddChild(reset);
        AddChild(row);
    }

    private void OnHexChanged(string text)
    {
        var raw = text.Trim();
        if (raw.Length == 0)
        {
            SetColor(null);
            return;
        }

        if (!raw.StartsWith('#'))
            raw = "#" + raw;

        if (Color.TryFromHex(raw) is { } parsed)
            SetColor(parsed);
    }

    private void SetColor(Color? color)
    {
        _previewStyle.BackgroundColor = color ?? Color.Transparent;
        _preview.PanelOverride = _previewStyle;
        ColorChanged?.Invoke(color);
    }

    private static string ToHex(Color c)
    {
        static int B(float v) => (int) MathF.Round(Math.Clamp(v, 0f, 1f) * 255f);
        return $"#{B(c.R):X2}{B(c.G):X2}{B(c.B):X2}";
    }
}
