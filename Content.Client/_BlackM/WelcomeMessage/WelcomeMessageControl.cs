using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._BlackM.WelcomeMessage;

public enum WelcomeSound
{
    Appear,
    Type,
    Disappear
}

public sealed class WelcomeMessageControl : Control
{
    private const float TopOffset = 280f;
    private const float BackgroundAlpha = 0.55f;
    private const float TextWidth = 340f;
    private const float PortraitScale = 4f;
    private const float CharsPerSecond = 38f;
    private const float FadeIn = 0.5f;
    private const float TypeDelay = 0.6f;
    private const float FadeOut = 0.7f;
    private const int TitleSize = 13;
    private const int BodySize = 12;

    private readonly Random _rng = new();
    private readonly string _text;
    private readonly string _fontPath;
    private readonly RichTextLabel _typed;
    private readonly Control _content;
    private readonly float _typeStart;
    private readonly float _typeEnd;
    private readonly float _total;

    private float _time;
    private int _lastShown = -1;
    private bool _lastCursor;
    private float _nextGlitch = 3f;
    private float _glitchUntil;

    private bool _appearPlayed;
    private bool _disappearPlayed;

    public bool Finished { get; private set; }

    public event Action<WelcomeSound>? PlaySound;

    public WelcomeMessageControl(string sender, string title, string text, Texture portrait,
        float hold, Font font, string fontPath)
    {
        _text = text;
        _fontPath = fontPath;
        _typeStart = FadeIn + TypeDelay;
        _typeEnd = _typeStart + text.Length / CharsPerSecond;
        _total = _typeEnd + hold;

        MouseFilter = MouseFilterMode.Ignore;
        Modulate = Color.White.WithAlpha(0f);

        var panel = new PanelContainer
        {
            HorizontalAlignment = HAlignment.Left,
            VerticalAlignment = VAlignment.Top,
            Margin = new Thickness(0, TopOffset, 0, 0),
            MouseFilter = MouseFilterMode.Ignore,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.Black.WithAlpha(BackgroundAlpha),
                ContentMarginLeftOverride = 6,
                ContentMarginRightOverride = 8,
                ContentMarginTopOverride = 5,
                ContentMarginBottomOverride = 5
            }
        };

        _content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8
        };

        var left = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalAlignment = VAlignment.Top
        };

        var portraitPanel = new PanelContainer
        {
            HorizontalAlignment = HAlignment.Center,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#0b3d2b"),
                BorderColor = Color.FromHex("#4fd6a0").WithAlpha(0.6f),
                BorderThickness = new Thickness(1)
            }
        };
        portraitPanel.AddChild(new TextureRect
        {
            Texture = portrait,
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            MinSize = new Vector2(portrait.Width, portrait.Height) * PortraitScale,
            Modulate = Color.FromHex("#bfffe0")
        });
        portraitPanel.AddChild(new VhsOverlay(strong: true));
        left.AddChild(portraitPanel);

        var plate = new PanelContainer
        {
            HorizontalAlignment = HAlignment.Center,
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.Black.WithAlpha(0.85f) }
        };
        plate.AddChild(new Label
        {
            Text = sender,
            FontOverride = font,
            HorizontalAlignment = HAlignment.Center
        });
        left.AddChild(plate);

        var right = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            MinWidth = TextWidth,
            MaxWidth = TextWidth
        };

        var titleBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalAlignment = HAlignment.Left
        };
        var titleLabel = new RichTextLabel();
        titleLabel.SetMessage(Msg(title.ToUpperInvariant(), TitleSize));
        titleBox.AddChild(titleLabel);
        titleBox.AddChild(new PanelContainer
        {
            MinHeight = 1,
            HorizontalExpand = true,
            Margin = new Thickness(0, 1, 0, 4),
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.White.WithAlpha(0.9f) }
        });
        right.AddChild(titleBox);

        _typed = new RichTextLabel();
        right.AddChild(_typed);

        _content.AddChild(left);
        _content.AddChild(right);
        panel.AddChild(_content);

        panel.AddChild(new VhsOverlay(strong: false));

        AddChild(panel);
    }

    private FormattedMessage Msg(string raw, int size, string? tail = null)
    {
        var esc = FormattedMessage.EscapeText(raw);
        return FormattedMessage.FromMarkupPermissive(
            $"[font=\"{_fontPath}\" size={size}]{esc}{tail}[/font]");
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (Finished)
            return;

        _time += args.DeltaSeconds;

        if (_time >= _total)
        {
            Finished = true;
            Visible = false;
            return;
        }

        if (!_appearPlayed)
        {
            _appearPlayed = true;
            PlaySound?.Invoke(WelcomeSound.Appear);
        }

        if (!_disappearPlayed && _time > _total - FadeOut)
        {
            _disappearPlayed = true;
            PlaySound?.Invoke(WelcomeSound.Disappear);
        }

        float alpha;
        if (_time < FadeIn)
            alpha = _time / FadeIn;
        else if (_time > _total - FadeOut)
            alpha = Math.Max(0f, (_total - _time) / FadeOut);
        else
            alpha = 1f;

        var glitching = _time < FadeIn + 0.3f || _time < _glitchUntil;
        if (_time >= _nextGlitch && _time >= _glitchUntil)
        {
            _glitchUntil = _time + 0.12f;
            _nextGlitch = _time + 2.5f + (float) _rng.NextDouble() * 3f;
        }

        var flicker = glitching
            ? 0.55f + (float) _rng.NextDouble() * 0.45f
            : 0.94f + (float) _rng.NextDouble() * 0.06f;
        Modulate = Color.White.WithAlpha(alpha * flicker);

        if (glitching)
        {
            var dx = (_rng.NextDouble() * 2 - 1) * 4;
            _content.Margin = new Thickness((float) Math.Max(0, dx), 0, (float) Math.Max(0, -dx), 0);
        }
        else if (_content.Margin != default)
        {
            _content.Margin = default;
        }

        var shown = _time < _typeStart
            ? 0
            : Math.Min(_text.Length, (int) ((_time - _typeStart) * CharsPerSecond));

        var cursorOn = _time >= _typeStart && (int) (_time * 2) % 2 == 0;

        if (shown != _lastShown || cursorOn != _lastCursor)
        {
            if (shown > _lastShown && shown > 0 && shown % 2 == 0 && !char.IsWhiteSpace(_text[shown - 1]))
                PlaySound?.Invoke(WelcomeSound.Type);

            _lastShown = shown;
            _lastCursor = cursorOn;
            var tail = _time >= _typeStart
                ? cursorOn ? "_" : "[color=#ffffff00]_[/color]"
                : null;
            _typed.SetMessage(Msg(_text.Substring(0, shown), BodySize, tail));
        }
    }
}

public sealed class VhsOverlay : Control
{
    private readonly bool _strong;
    private float _t;

    public VhsOverlay(bool strong)
    {
        _strong = strong;
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _t += args.DeltaSeconds;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var w = PixelWidth;
        var h = PixelHeight;
        if (w <= 0 || h <= 0)
            return;

        var lineH = Math.Max(1, (int) UIScale);
        var step = Math.Max(2, (int) ((_strong ? 2f : 3f) * UIScale));
        var color = _strong
            ? Color.FromHex("#031a10").WithAlpha(0.55f)
            : Color.Black.WithAlpha(0.22f);

        for (var y = 0; y < h; y += step)
            handle.DrawRect(UIBox2.FromDimensions(0, y, w, lineH), color);

        var barH = h * 0.3f;
        var pos = (_t * 0.3f % 1.5f - 0.3f) * h;
        var top = Math.Max(0f, pos);
        var bottom = Math.Min(h, pos + barH);
        if (bottom > top)
        {
            handle.DrawRect(UIBox2.FromDimensions(0, top, w, bottom - top),
                Color.FromHex("#a8ffd8").WithAlpha(_strong ? 0.10f : 0.05f));
        }
    }
}