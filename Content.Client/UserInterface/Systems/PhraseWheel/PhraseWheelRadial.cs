using System.Numerics;
using Content.Client._BlackM.PhraseWheel;
using Content.Shared._BlackM.PhraseWheel;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.PhraseWheel;

public sealed record PhraseWheelCategoryView(
    string Id,
    string Name,
    SpriteSpecifier? Icon,
    Color Accent,
    IReadOnlyList<PhraseWheelEntryPrototype> Phrases);

public sealed class PhraseWheelRadial : Control
{
    public const float WheelSize = 480f;

    private const float HubRadius = 62f;
    private const float CategoryInner = 70f;
    private const float CategoryOuter = 132f;
    private const float PhraseInner = 140f;
    private const float PhraseOuter = 236f;

    private const float SectorGap = 0.035f;
    private const float ArcStep = 0.1f;
    private const int MaxArcSteps = 64;
    private const int PhrasesPerPage = 10;

    private static readonly Color PanelColor = new(0.07f, 0.07f, 0.09f, 0.88f);

    private enum HoverKind : byte { None, Hub, Category, Phrase }

    public event Action<PhraseWheelEntryPrototype>? OnPhraseSelected;
    public event Action? OnCloseRequested;

    private readonly IResourceCache _resCache;
    private readonly IReadOnlyList<PhraseWheelCategoryView> _categories;
    private readonly Func<float>? _remainingCooldown;

    private readonly Vector2[] _verts = new Vector2[MaxArcSteps * 6];

    private readonly List<Control> _categoryWidgets = new();
    private readonly List<Control> _phraseWidgets = new();
    private readonly List<PhraseWheelEntryPrototype> _pagePhrases = new();

    private readonly BoxContainer _hubBox;
    private readonly RichTextLabel _hubMain;
    private readonly RichTextLabel _hubSub;

    private int _activeCategory;
    private int _page;
    private HoverKind _hoverKind;
    private int _hoverIndex;
    private int _cooldownTenths;

    public string ActiveCategoryId => _categories[_activeCategory].Id;

    public PhraseWheelRadial(
        IReadOnlyList<PhraseWheelCategoryView> categories,
        IResourceCache resCache,
        Func<float>? remainingCooldown,
        int initialCategory)
    {
        _categories = categories;
        _resCache = resCache;
        _remainingCooldown = remainingCooldown;
        _activeCategory = Math.Clamp(initialCategory, 0, Math.Max(0, categories.Count - 1));

        MinSize = new Vector2(WheelSize, WheelSize);
        MouseFilter = MouseFilterMode.Stop;

        _hubMain = new RichTextLabel
        {
            HorizontalAlignment = HAlignment.Center,
            MaxWidth = 100f,
            MouseFilter = MouseFilterMode.Ignore,
        };
        _hubSub = new RichTextLabel
        {
            HorizontalAlignment = HAlignment.Center,
            MaxWidth = 100f,
            Modulate = new Color(0.7f, 0.7f, 0.76f),
            MouseFilter = MouseFilterMode.Ignore,
        };
        _hubBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 2,
            MouseFilter = MouseFilterMode.Ignore,
        };
        _hubBox.AddChild(_hubMain);
        _hubBox.AddChild(_hubSub);
        AddChild(_hubBox);

        foreach (var cat in _categories)
        {
            var widget = MakeSlotWidget(PhraseWheelTextures.Resolve(_resCache, cat.Icon), cat.Name, 28f, 64f);
            AddChild(widget);
            _categoryWidgets.Add(widget);
        }

        _cooldownTenths = CooldownToTenths(_remainingCooldown?.Invoke() ?? 0f);
        RebuildPhraseWidgets();
        RefreshHub();
    }

    private static float SectorWidth(int count) => MathF.Tau / count;

    private static float SectorAngle(int index, int count) => -MathF.PI / 2f + index * SectorWidth(count);

    private static Vector2 Polar(float angle, float radius) => new(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);

    private static int SectorIndexAt(float angle, int count)
    {
        var width = SectorWidth(count);
        var t = (angle + MathF.PI / 2f + width / 2f) % MathF.Tau;
        if (t < 0)
            t += MathF.Tau;

        return Math.Min((int) (t / width), count - 1);
    }

    private (HoverKind Kind, int Index) HitTest(Vector2 pos)
    {
        var v = pos - Size / 2f;
        var dist = v.Length();

        if (dist <= HubRadius)
            return (HoverKind.Hub, 0);

        var angle = MathF.Atan2(v.Y, v.X);

        if (dist >= CategoryInner && dist <= CategoryOuter && _categories.Count > 0)
            return (HoverKind.Category, SectorIndexAt(angle, _categories.Count));

        if (dist >= PhraseInner && dist <= PhraseOuter && _pagePhrases.Count > 0)
            return (HoverKind.Phrase, SectorIndexAt(angle, _pagePhrases.Count));

        return (HoverKind.None, 0);
    }

    private static Control MakeSlotWidget(Texture? icon, string text, float iconSize, float maxTextWidth)
    {
        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 2,
            MouseFilter = MouseFilterMode.Ignore,
        };

        if (icon != null)
        {
            box.AddChild(new TextureRect
            {
                Texture = icon,
                MinSize = new Vector2(iconSize, iconSize),
                MaxSize = new Vector2(iconSize, iconSize),
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                HorizontalAlignment = HAlignment.Center,
                MouseFilter = MouseFilterMode.Ignore,
            });
        }

        var label = new RichTextLabel
        {
            HorizontalAlignment = HAlignment.Center,
            MaxWidth = maxTextWidth,
            MouseFilter = MouseFilterMode.Ignore,
        };
        label.SetMessage(FormattedMessage.FromUnformatted(text));
        box.AddChild(label);

        return box;
    }

    private static int PageCount(PhraseWheelCategoryView category)
    {
        return Math.Max(1, (category.Phrases.Count + PhrasesPerPage - 1) / PhrasesPerPage);
    }

    private void RebuildPhraseWidgets()
    {
        foreach (var widget in _phraseWidgets)
            RemoveChild(widget);

        _phraseWidgets.Clear();
        _pagePhrases.Clear();

        var category = _categories[_activeCategory];
        var start = _page * PhrasesPerPage;
        var end = Math.Min(category.Phrases.Count, start + PhrasesPerPage);

        for (var i = start; i < end; i++)
        {
            var phrase = category.Phrases[i];
            _pagePhrases.Add(phrase);

            var icon = PhraseWheelTextures.Resolve(_resCache, phrase.Icon ?? category.Icon);
            var text = string.IsNullOrEmpty(phrase.Label) ? phrase.Text : phrase.Label;

            var widget = MakeSlotWidget(icon, text, 32f, 80f);
            AddChild(widget);
            _phraseWidgets.Add(widget);
        }

        ApplyCooldownVisual();
    }

    private void ApplyCooldownVisual()
    {
        var modulate = _cooldownTenths > 0 ? new Color(1f, 1f, 1f, 0.45f) : Color.White;
        foreach (var widget in _phraseWidgets)
            widget.Modulate = modulate;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var center = finalSize / 2f;

        ArrangeCentered(_hubBox, center);

        var catRadius = (CategoryInner + CategoryOuter) / 2f;
        for (var i = 0; i < _categoryWidgets.Count; i++)
            ArrangeCentered(_categoryWidgets[i], center + Polar(SectorAngle(i, _categoryWidgets.Count), catRadius));

        var phraseRadius = (PhraseInner + PhraseOuter) / 2f;
        for (var i = 0; i < _phraseWidgets.Count; i++)
            ArrangeCentered(_phraseWidgets[i], center + Polar(SectorAngle(i, _phraseWidgets.Count), phraseRadius));

        return finalSize;
    }

    private static void ArrangeCentered(Control child, Vector2 centerPos)
    {
        var size = child.DesiredSize;
        child.Arrange(UIBox2.FromDimensions(centerPos - size / 2f, size));
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var scale = UIScale;
        var center = Size * scale / 2f;
        var active = _categories[_activeCategory];
        var onCooldown = _cooldownTenths > 0;

        var catCount = _categories.Count;
        for (var i = 0; i < catCount; i++)
        {
            var accent = _categories[i].Accent;
            Color fill;
            if (i == _activeCategory)
                fill = Mix(PanelColor, accent, 0.50f);
            else if (_hoverKind == HoverKind.Category && _hoverIndex == i)
                fill = Mix(PanelColor, accent, 0.30f);
            else
                fill = PanelColor;

            DrawSector(handle, center, CategoryInner, CategoryOuter,
                SectorAngle(i, catCount), SectorWidth(catCount), fill, scale);
        }

        var phraseCount = _pagePhrases.Count;
        for (var i = 0; i < phraseCount; i++)
        {
            var accent = _pagePhrases[i].Color ?? active.Accent;
            var hovered = _hoverKind == HoverKind.Phrase && _hoverIndex == i;

            Color fill;
            if (onCooldown)
                fill = hovered ? Mix(PanelColor, accent, 0.20f) : WithAlpha(PanelColor, 0.6f);
            else
                fill = hovered ? Mix(PanelColor, accent, 0.55f) : Mix(PanelColor, accent, 0.12f);

            DrawSector(handle, center, PhraseInner, PhraseOuter,
                SectorAngle(i, phraseCount), SectorWidth(phraseCount), fill, scale);
        }

        var hubFill = _hoverKind == HoverKind.Hub ? Mix(PanelColor, active.Accent, 0.25f) : PanelColor;
        handle.DrawCircle(center, HubRadius * scale, hubFill);
        handle.DrawCircle(center, HubRadius * scale, active.Accent, filled: false);
    }

    private void DrawSector(DrawingHandleScreen handle, Vector2 center, float innerR, float outerR,
        float angle, float width, Color color, float scale)
    {
        var a0 = angle - width / 2f + SectorGap / 2f;
        var a1 = angle + width / 2f - SectorGap / 2f;
        if (a1 <= a0)
            return;

        var steps = Math.Clamp((int) MathF.Ceiling((a1 - a0) / ArcStep), 2, MaxArcSteps);
        var count = 0;

        for (var s = 0; s < steps; s++)
        {
            var b0 = a0 + (a1 - a0) * s / steps;
            var b1 = a0 + (a1 - a0) * (s + 1) / steps;

            var in0 = center + Polar(b0, innerR * scale);
            var out0 = center + Polar(b0, outerR * scale);
            var in1 = center + Polar(b1, innerR * scale);
            var out1 = center + Polar(b1, outerR * scale);

            _verts[count++] = in0;
            _verts[count++] = out0;
            _verts[count++] = out1;
            _verts[count++] = in0;
            _verts[count++] = out1;
            _verts[count++] = in1;
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _verts.AsSpan(0, count), color);
    }

    private static Color Mix(Color a, Color b, float t)
    {
        return new Color(
            a.R + (b.R - a.R) * t,
            a.G + (b.G - a.G) * t,
            a.B + (b.B - a.B) * t,
            a.A + (b.A - a.A) * t);
    }

    private static Color WithAlpha(Color c, float alpha) => new(c.R, c.G, c.B, alpha);

    private static int CooldownToTenths(float seconds)
    {
        return seconds > 0f ? (int) MathF.Ceiling(seconds * 10f) : 0;
    }

    private static string? ChatHint(PhraseWheelChatType type) => type switch
    {
        PhraseWheelChatType.Whisper => Loc.GetString("phrase-wheel-hint-whisper"),
        PhraseWheelChatType.Emote => Loc.GetString("phrase-wheel-hint-emote"),
        PhraseWheelChatType.Shout => Loc.GetString("phrase-wheel-hint-shout"),
        _ => null,
    };

    private void RefreshHub()
    {
        var category = _categories[_activeCategory];
        string main;
        string? sub = null;

        switch (_hoverKind)
        {
            case HoverKind.Phrase when _hoverIndex < _pagePhrases.Count:
            {
                var phrase = _pagePhrases[_hoverIndex];
                main = phrase.Text;
                sub = ChatHint(phrase.ChatType);
                break;
            }
            case HoverKind.Category when _hoverIndex < _categories.Count:
                main = _categories[_hoverIndex].Name;
                break;
            case HoverKind.Hub:
                main = Loc.GetString("phrase-wheel-close");
                break;
            default:
            {
                main = category.Phrases.Count == 0 ? Loc.GetString("phrase-wheel-empty") : category.Name;
                var pages = PageCount(category);
                if (pages > 1)
                    sub = Loc.GetString("phrase-wheel-page", ("page", _page + 1), ("pages", pages));
                break;
            }
        }

        if (_cooldownTenths > 0 && (_hoverKind == HoverKind.Phrase || _hoverKind == HoverKind.None))
        {
            var cooldown = Loc.GetString("phrase-wheel-cooldown", ("seconds", (_cooldownTenths / 10f).ToString("0.0")));
            sub = sub == null ? cooldown : $"{sub} · {cooldown}";
        }

        _hubMain.SetMessage(FormattedMessage.FromUnformatted(main));

        if (sub == null)
        {
            _hubSub.Visible = false;
        }
        else
        {
            _hubSub.Visible = true;
            _hubSub.SetMessage(FormattedMessage.FromUnformatted(sub));
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var tenths = CooldownToTenths(_remainingCooldown?.Invoke() ?? 0f);
        if (tenths == _cooldownTenths)
            return;

        var wasOnCooldown = _cooldownTenths > 0;
        _cooldownTenths = tenths;

        if (wasOnCooldown != (tenths > 0))
            ApplyCooldownVisual();

        RefreshHub();
    }

    private void SetHover(HoverKind kind, int index)
    {
        if (kind == _hoverKind && index == _hoverIndex)
            return;

        _hoverKind = kind;
        _hoverIndex = index;
        RefreshHub();
    }

    private void SelectCategory(int index)
    {
        if (index == _activeCategory)
            return;

        _activeCategory = index;
        _page = 0;
        _hoverKind = HoverKind.None;
        RebuildPhraseWidgets();
        RefreshHub();
    }

    private void ChangePage(int delta)
    {
        var pages = PageCount(_categories[_activeCategory]);
        if (pages <= 1)
            return;

        _page = (_page + delta + pages) % pages;
        _hoverKind = HoverKind.None;
        RebuildPhraseWidgets();
        RefreshHub();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        var (kind, index) = HitTest(args.RelativePosition);
        SetHover(kind, index);
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        SetHover(HoverKind.None, 0);
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);

        if (args.Delta.Y == 0f)
            return;

        ChangePage(args.Delta.Y < 0f ? 1 : -1);
        args.Handle();
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function == EngineKeyFunctions.UIClick || args.Function == EngineKeyFunctions.UIRightClick)
            args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function == EngineKeyFunctions.UIRightClick)
        {
            OnCloseRequested?.Invoke();
            args.Handle();
            return;
        }

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        args.Handle();

        var (kind, index) = HitTest(args.RelativePosition);
        switch (kind)
        {
            case HoverKind.Hub:
                OnCloseRequested?.Invoke();
                break;

            case HoverKind.Category:
                SelectCategory(index);
                break;

            case HoverKind.Phrase when _cooldownTenths <= 0 && index < _pagePhrases.Count:
                OnPhraseSelected?.Invoke(_pagePhrases[index]);
                break;
        }
    }
}
