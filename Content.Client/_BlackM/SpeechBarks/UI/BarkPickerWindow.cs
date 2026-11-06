using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Content.Shared._BlackM.SpeechBarks;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._BlackM.SpeechBarks.UI;

public sealed class BarkPickerWindow : DefaultWindow
{
    private static readonly Regex CategoryRegex = new(@"\[(?<cat>[^\]]+)\]\s*$");

    private static readonly Color SelectedColor = Color.FromHex("#3B5B8C");
    private static readonly Color RowColor = Color.FromHex("#25252A");

    public event Action<string>? OnBarkSelected;
    public event Action<string>? OnBarkPreview;

    private readonly LineEdit _search;
    private readonly OptionButton _category;
    private readonly BoxContainer _list;
    private readonly Label _empty;

    private readonly List<Entry> _entries = new();
    private readonly Dictionary<string, Row> _rows = new();
    private readonly List<string> _categories = new();
    private string? _selected;

    public BarkPickerWindow(IReadOnlyList<BlackMBarkPrototype> barks)
    {
        Title = Loc.GetString("blackm-bark-picker-title");
        MinSize = new Vector2(460, 520);
        SetSize = new Vector2(460, 560);

        foreach (var bark in barks)
        {
            var name = Loc.GetString(bark.Name);
            var match = CategoryRegex.Match(name);
            var category = match.Success ? match.Groups["cat"].Value.Trim() : bark.Category.Replace('_', ' ');
            _entries.Add(new Entry(bark.ID, name, category));
        }

        _categories.AddRange(_entries
            .Select(e => e.Category)
            .GroupBy(c => c.ToLowerInvariant())
            .Select(g => g.First())
            .OrderBy(c => c.ToLowerInvariant()));

        _search = new LineEdit
        {
            HorizontalExpand = true,
            PlaceHolder = Loc.GetString("blackm-bark-picker-search"),
        };
        _search.OnTextChanged += _ => Rebuild();

        _category = new OptionButton { MinWidth = 140, Margin = new Thickness(5, 0, 0, 0) };
        _category.AddItem(Loc.GetString("blackm-bark-picker-all"), 0);
        for (var i = 0; i < _categories.Count; i++)
        {
            _category.AddItem(_categories[i], i + 1);
        }

        _category.OnItemSelected += args =>
        {
            _category.SelectId(args.Id);
            Rebuild();
        };

        _list = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 2,
        };

        _empty = new Label
        {
            Text = Loc.GetString("blackm-bark-picker-empty"),
            HorizontalAlignment = HAlignment.Center,
            Visible = false,
            Margin = new Thickness(0, 10, 0, 0),
        };

        var top = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };
        top.AddChild(_search);
        top.AddChild(_category);

        var scroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HScrollEnabled = false,
            Margin = new Thickness(0, 5, 0, 0),
        };

        var listHolder = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };
        listHolder.AddChild(_list);
        listHolder.AddChild(_empty);
        scroll.AddChild(listHolder);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        root.AddChild(top);
        root.AddChild(scroll);

        Contents.AddChild(root);
        Rebuild();
    }

    public void SetSelected(string protoId)
    {
        if (_selected == protoId)
            return;

        var previous = _selected;
        _selected = protoId;

        if (previous != null && _rows.TryGetValue(previous, out var oldRow))
            ApplyRowState(oldRow, false);

        if (_rows.TryGetValue(protoId, out var newRow))
            ApplyRowState(newRow, true);
    }

    private static void ApplyRowState(Row row, bool selected)
    {
        row.Style.BackgroundColor = selected ? SelectedColor : RowColor;
        row.Select.Text = Loc.GetString(selected ? "blackm-bark-picker-selected" : "blackm-bark-picker-select");
        row.Select.Disabled = selected;
    }

    private void Rebuild()
    {
        _list.RemoveAllChildren();
        _rows.Clear();

        var filter = _search.Text.Trim();
        var category = _category.SelectedId > 0 ? _categories[_category.SelectedId - 1] : null;
        var shown = 0;

        foreach (var entry in _entries)
        {
            if (category != null && !entry.Category.Equals(category, StringComparison.CurrentCultureIgnoreCase))
                continue;

            if (filter.Length > 0 &&
                !entry.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase) &&
                !entry.Id.Contains(filter, StringComparison.OrdinalIgnoreCase))
                continue;

            _list.AddChild(BuildRow(entry));
            shown++;
        }

        _empty.Visible = shown == 0;
    }

    private Control BuildRow(Entry entry)
    {
        var style = new StyleBoxFlat
        {
            BackgroundColor = RowColor,
            ContentMarginLeftOverride = 4,
            ContentMarginRightOverride = 4,
            ContentMarginTopOverride = 2,
            ContentMarginBottomOverride = 2,
        };

        var panel = new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = style,
        };

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };

        var play = new Button
        {
            Text = ">",
            MinWidth = 32,
            ToolTip = Loc.GetString("blackm-bark-play"),
        };
        play.OnPressed += _ => OnBarkPreview?.Invoke(entry.Id);

        var name = new Label
        {
            Text = entry.Name,
            HorizontalExpand = true,
            ClipText = true,
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(6, 0, 6, 0),
        };

        var select = new Button { MinWidth = 90 };
        select.OnPressed += _ =>
        {
            OnBarkSelected?.Invoke(entry.Id);
            OnBarkPreview?.Invoke(entry.Id);
        };

        row.AddChild(play);
        row.AddChild(name);
        row.AddChild(select);
        panel.AddChild(row);

        var controls = new Row(style, select);
        ApplyRowState(controls, entry.Id == _selected);
        _rows[entry.Id] = controls;
        return panel;
    }

    private sealed record Entry(string Id, string Name, string Category);

    private sealed record Row(StyleBoxFlat Style, Button Select);
}
