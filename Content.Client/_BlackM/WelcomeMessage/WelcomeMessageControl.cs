using System.Numerics;
using System.Text;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.WelcomeMessage;

public enum WelcomeSound : byte
{
    Appear,
    Type,
    Disappear,
}

public sealed record WelcomeFonts(Font Caption, Font Sender, Font Body);

public sealed record HudMessage(
    string Sender,
    string Title,
    string Text,
    Texture? Portrait,
    EntityUid? PortraitEntity,
    float Duration,
    Color? ChannelColor = null);

public sealed class WelcomeMessageControl : Control
{
    private static readonly Color BodyColor = Color.FromHex("#EDEDED");

    public const float LeftMargin = 14f;
    private const float PortraitWidth = 112f;
    private const float PortraitHeight = 96f;
    private const float PanelPadding = 8f;
    private const int ColumnGap = 6;
    private const float SenderPadding = 4f;
    private const float MaxTextWidth = 560f;

    private const float OpenTime = 0.20f;
    private const float TypeDelay = 0.05f;
    private const float CloseTime = 0.22f;

    private const float CharsPerSecond = 70f;
    private const int CharsPerTypeSound = 5;
    private const float MinTypeSoundGap = 0.09f;

    private const float MinHold = 3f;
    private const float ReadCharsPerSecond = 25f;

    private enum Phase : byte
    {
        Open,
        Type,
        Hold,
        Close,
        Done,
    }

    public event Action<WelcomeSound>? PlaySound;

    public bool Finished => _phase == Phase.Done;

    private readonly BoxContainer _content;
    private readonly List<(Label Label, string Text)> _lines = new();
    private readonly string _fullText;

    private Phase _phase = Phase.Open;
    private float _phaseTime;
    private float _typed;
    private int _shown;
    private int _soundCounter;
    private float _sinceTypeSound = MinTypeSoundGap;
    private float _holdLeft;
    private bool _hovered;
    private bool _appearPlayed;
    private bool _typeSoundPlayed;

    private float _reveal;
    private float _laidOut = -1f;

    public WelcomeMessageControl(HudMessage msg, WelcomeFonts fonts, float viewportWidth, float uiScale)
    {
        MouseFilter = MouseFilterMode.Stop;
        RectClipContent = true;
        Modulate = Color.White.WithAlpha(0f);

        _content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = ColumnGap,
            Margin = new Thickness(PanelPadding),
        };
        AddChild(_content);

        var hasPortrait = msg.Portrait != null || msg.PortraitEntity != null;
        var hasSender = !string.IsNullOrWhiteSpace(msg.Sender);
        var hasSenderColumn = hasPortrait || hasSender;
        var availableTextWidth = viewportWidth - 2f * (LeftMargin + PanelPadding);
        if (hasSenderColumn)
            availableTextWidth -= PortraitWidth + ColumnGap;
        var textWidth = viewportWidth > 0f ? Math.Clamp(availableTextWidth, 1f, MaxTextWidth) : MaxTextWidth;

        if (hasSenderColumn)
        {
            var senderColumn = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                SeparationOverride = 0,
                SetWidth = PortraitWidth,
                VerticalAlignment = VAlignment.Top,
            };
            _content.AddChild(senderColumn);

            if (hasPortrait)
            {
                senderColumn.AddChild(new PortraitView(msg.Portrait, msg.PortraitEntity, msg.ChannelColor)
                {
                    SetHeight = PortraitHeight,
                });
            }

            if (hasSender)
            {
                senderColumn.AddChild(new HudLabel
                {
                    Text = string.Join('\n', Wrap(msg.Sender, fonts.Sender, PortraitWidth - 2f * SenderPadding, uiScale)),
                    FontOverride = fonts.Sender,
                    FontColorOverride = BodyColor,
                    Align = Label.AlignMode.Center,
                    Margin = new Thickness(SenderPadding, 2, SenderPadding, 2),
                });
            }
        }

        var titleLines = Wrap(msg.Title.ToUpperInvariant(), fonts.Caption, textWidth, uiScale);
        var bodyLines = msg.Text.Length == 0 ? new List<string>() : Wrap(msg.Text, fonts.Body, textWidth, uiScale);
        var width = 0f;
        foreach (var line in titleLines)
            width = MathF.Max(width, MeasureWidth(line, fonts.Caption, uiScale));
        foreach (var line in bodyLines)
            width = MathF.Max(width, MeasureWidth(line, fonts.Body, uiScale));
        width = MathF.Min(MathF.Ceiling(width), textWidth);

        var column = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 2,
            SetWidth = width,
            VerticalAlignment = VAlignment.Top,
        };
        _content.AddChild(column);

        if (!string.IsNullOrWhiteSpace(msg.Title))
        {
            var caption = new PanelContainer
            {
                HorizontalAlignment = HAlignment.Left,
                PanelOverride = new StyleBoxFlat
                {
                    BorderColor = Color.White,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                },
            };
            column.AddChild(caption);
            caption.AddChild(new HudLabel
            {
                Text = string.Join('\n', titleLines),
                FontOverride = fonts.Caption,
                FontColorOverride = Color.White,
            });
        }

        var lineHeight = fonts.Body.GetHeight(uiScale) / uiScale;
        foreach (var line in bodyLines)
        {
            var label = new HudLabel
            {
                Text = string.Empty,
                FontOverride = fonts.Body,
                FontColorOverride = BodyColor,
                MinHeight = lineHeight,
            };
            column.AddChild(label);
            _lines.Add((label, line));
        }

        _fullText = string.Concat(bodyLines);
        _holdLeft = MathF.Max(MathF.Max(msg.Duration, MinHold), 2f + _fullText.Length / ReadCharsPerSecond);
    }

    public void Skip()
    {
        switch (_phase)
        {
            case Phase.Open:
            case Phase.Type:
                _appearPlayed = true;
                _reveal = 1f;
                Reveal(_fullText.Length, false);
                _typed = _fullText.Length;
                SetPhase(Phase.Hold);
                break;
            case Phase.Hold:
                BeginClose();
                break;
        }
    }

    public void ForceClose()
    {
        SetPhase(Phase.Done);
        Modulate = Color.White.WithAlpha(0f);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick && args.Function != EngineKeyFunctions.UIRightClick)
            return;

        args.Handle();
        Skip();
    }

    protected override void MouseEntered()
    {
        base.MouseEntered();
        _hovered = true;
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        _hovered = false;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var dt = args.DeltaSeconds;
        _phaseTime += dt;
        _sinceTypeSound += dt;

        switch (_phase)
        {
            case Phase.Open:
                if (!_appearPlayed)
                {
                    _appearPlayed = true;
                    PlaySound?.Invoke(WelcomeSound.Appear);
                }

                _reveal = EaseOut(_phaseTime / OpenTime);

                if (_phaseTime >= OpenTime + TypeDelay)
                    SetPhase(_fullText.Length > 0 ? Phase.Type : Phase.Hold);
                break;

            case Phase.Type:
                _typed += dt * CharsPerSecond;
                Reveal(Math.Min(_fullText.Length, (int) _typed), true);

                if (_shown >= _fullText.Length)
                    SetPhase(Phase.Hold);
                break;

            case Phase.Hold:
                if (!_hovered)
                    _holdLeft -= dt;

                if (_holdLeft <= 0f)
                    BeginClose();
                break;

            case Phase.Close:
                if (_phaseTime >= CloseTime)
                    SetPhase(Phase.Done);
                break;
        }

        UpdateVisuals();
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        _content.Measure(Vector2Helpers.Infinity);

        var full = _content.DesiredSize;
        var height = MathF.Max(2f, full.Y * _reveal);
        return new Vector2(MathF.Round(full.X), MathF.Round(height));
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var full = _content.DesiredSize;
        _content.Arrange(UIBox2.FromDimensions(Vector2.Zero, full));
        return finalSize;
    }

    private void SetPhase(Phase phase)
    {
        _phase = phase;
        _phaseTime = 0f;
    }

    private void BeginClose()
    {
        if (_phase is Phase.Close or Phase.Done)
            return;

        MouseFilter = MouseFilterMode.Ignore;
        _hovered = false;
        PlaySound?.Invoke(WelcomeSound.Disappear);
        SetPhase(Phase.Close);
    }

    private void Reveal(int count, bool sounds)
    {
        count = Math.Clamp(count, 0, _fullText.Length);
        if (count <= _shown)
            return;

        var start = 0;
        foreach (var (label, line) in _lines)
        {
            var end = start + line.Length;
            if (count > start && _shown < end)
            {
                var visible = Math.Clamp(count - start, 0, line.Length);
                if (visible > 0 && visible < line.Length && char.IsHighSurrogate(line[visible - 1]))
                    visible--;

                label.Text = line.Substring(0, visible);
            }

            start = end;
        }

        if (sounds)
        {
            for (var i = _shown; i < count; i++)
            {
                if (!char.IsWhiteSpace(_fullText[i]))
                    _soundCounter++;
            }

            if (_soundCounter > 0 && (!_typeSoundPlayed || _soundCounter >= CharsPerTypeSound) &&
                _sinceTypeSound >= MinTypeSoundGap)
            {
                _typeSoundPlayed = true;
                _soundCounter = 0;
                _sinceTypeSound = 0f;
                PlaySound?.Invoke(WelcomeSound.Type);
            }
        }

        _shown = count;
    }

    private void UpdateVisuals()
    {
        var alpha = _phase switch
        {
            Phase.Open => Math.Clamp(_phaseTime / (OpenTime * 0.6f), 0f, 1f),
            Phase.Close => 1f - EaseInOut(_phaseTime / CloseTime),
            Phase.Done => 0f,
            _ => 1f,
        };
        Modulate = Color.White.WithAlpha(alpha);

        if (!_reveal.Equals(_laidOut))
        {
            _laidOut = _reveal;
            InvalidateMeasure();
        }
    }

    private static float EaseOut(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return 1f - MathF.Pow(1f - t, 3f);
    }

    private static float EaseInOut(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t < 0.5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3f) / 2f;
    }

    private static float MeasureWidth(string text, Font font, float uiScale)
    {
        var width = 0;
        foreach (var rune in text.EnumerateRunes())
            width += font.GetCharMetrics(rune, uiScale)?.Advance ?? 0;
        return width / uiScale;
    }

    private static List<string> Wrap(string text, Font font, float maxWidth, float uiScale)
    {
        var lines = new List<string>();

        foreach (var paragraph in text.Replace("\r", string.Empty).Split('\n'))
        {
            var current = string.Empty;

            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (current.Length > 0)
                {
                    var candidate = current + " " + word;
                    if (MeasureWidth(candidate, font, uiScale) <= maxWidth)
                    {
                        current = candidate;
                        continue;
                    }

                    lines.Add(current);
                    current = string.Empty;
                }

                if (MeasureWidth(word, font, uiScale) <= maxWidth)
                {
                    current = word;
                    continue;
                }

                var chunkStart = 0;
                var offset = 0;
                var chunkWidth = 0;
                foreach (var rune in word.EnumerateRunes())
                {
                    var advance = font.GetCharMetrics(rune, uiScale)?.Advance ?? 0;
                    if (offset > chunkStart && (chunkWidth + advance) / uiScale > maxWidth)
                    {
                        lines.Add(word[chunkStart..offset]);
                        chunkStart = offset;
                        chunkWidth = 0;
                    }

                    offset += rune.Utf16SequenceLength;
                    chunkWidth += advance;
                }

                current = word.Substring(chunkStart);
            }

            lines.Add(current);
        }

        return lines;
    }

    private sealed class HudLabel : Label
    {
        protected override void Draw(DrawingHandleScreen handle)
        {
            var transform = handle.GetTransform();
            var modulation = handle.Modulate;
            var offset = MathF.Max(1f, MathF.Round(UIScale));

            handle.SetTransform(Matrix3x2.CreateTranslation(offset, offset) * transform);
            handle.Modulate = Color.Black.WithAlpha(modulation.A);
            base.Draw(handle);

            handle.Modulate = modulation;
            handle.SetTransform(transform);
            base.Draw(handle);
        }
    }

    private sealed class PortraitView : Control
    {
        private static readonly Color DefaultCameraBackground = Color.FromHex("#AD5019");

        private const float EntityScale = 6f;
        private const float EntityViewSize = 32f * EntityScale;

        private readonly Texture? _texture;
        private readonly SpriteView? _spriteView;
        private readonly Color _background;
        private readonly VhsOverlay _vhs;

        public PortraitView(Texture? texture, EntityUid? entity, Color? channelColor)
        {
            _texture = texture;
            _background = channelColor is { } accent
                ? Color.InterpolateBetween(accent, Color.Black, 0.32f).WithAlpha(1f)
                : DefaultCameraBackground;
            _vhs = new VhsOverlay(channelColor);
            MouseFilter = MouseFilterMode.Ignore;
            RectClipContent = true;

            if (entity != null)
            {
                _spriteView = new SpriteView
                {
                    OverrideDirection = Direction.South,
                    Stretch = SpriteView.StretchMode.None,
                    Scale = new Vector2(EntityScale, EntityScale),
                    MouseFilter = MouseFilterMode.Ignore,
                };
                _spriteView.SetEntity(entity.Value);
                AddChild(_spriteView);
            }

            AddChild(_vhs);
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            if (_spriteView != null)
            {
                var x = MathF.Round((finalSize.X - EntityViewSize) / 2f);
                _spriteView.Arrange(UIBox2.FromDimensions(new Vector2(x, 2f), new Vector2(EntityViewSize)));
            }

            _vhs.Arrange(UIBox2.FromDimensions(Vector2.Zero, finalSize));
            return finalSize;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            handle.DrawRect(PixelSizeBox, _background);
            if (_texture == null || PixelHeight <= 0)
                return;

            var drawWidth = _texture.Width * (float) PixelHeight / _texture.Height;
            var left = MathF.Round((PixelWidth - drawWidth) / 2f);
            var size = new Vector2(drawWidth, PixelHeight);
            handle.DrawTextureRect(_texture, UIBox2.FromDimensions(new Vector2(left, 0f), size));
        }
    }

    private sealed class VhsOverlay(Color? channelColor) : Control
    {
        private readonly Color _scanlineColor = (channelColor is { } accent
            ? Color.InterpolateBetween(Color.Black, accent, 0.18f)
            : Color.FromHex("#2E1005")).WithAlpha(0.27f);

        private readonly Color _trackingColor = (channelColor is { } accent
            ? Color.InterpolateBetween(accent, Color.White, 0.60f)
            : Color.FromHex("#FFD191")).WithAlpha(0.19f);

        private float _elapsed;

        protected override void FrameUpdate(FrameEventArgs args)
        {
            _elapsed += args.DeltaSeconds;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            var width = PixelWidth;
            var height = PixelHeight;
            if (width <= 0 || height <= 0)
                return;

            var pixel = MathF.Max(1f, MathF.Round(UIScale));
            for (var y = 0f; y < height; y += 3f * pixel)
                handle.DrawRect(new UIBox2(0, y, width, MathF.Min(height, y + pixel)), _scanlineColor);

            var trackingY = MathF.Floor(_elapsed * 40f * UIScale) % height;
            handle.DrawRect(new UIBox2(0, trackingY, width, MathF.Min(height, trackingY + 3f * pixel)), _trackingColor);

            var frame = (int) (_elapsed * 12f);
            for (var i = 0; i < 5; i++)
            {
                var x = (frame * 43 + i * 67) % width;
                var y = (frame * 23 + i * 37) % height;
                var right = MathF.Min(width, x + (3 + (frame + i * 5) % 12) * pixel);
                handle.DrawRect(new UIBox2(x, y, right, MathF.Min(height, y + pixel)), _trackingColor);
            }
        }
    }
}
