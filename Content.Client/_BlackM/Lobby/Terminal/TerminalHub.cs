using System.Numerics;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._BlackM.Lobby.Terminal;

public sealed class TerminalHub : PanelContainer
{
    [Dependency] private readonly IResourceCache _cache = default!;

    public event Action<string?>? Activated;

    private sealed class Entry
    {
        public string? TabId;
        public ContainerButton Row = default!;
        public Label Marker = default!;
    }

    private readonly List<Entry> _entries = new();
    private readonly Label _title;
    private readonly BoxContainer _utilities;
    private int _selected = -1;

    public TerminalHub()
    {
        IoCManager.InjectDependencies(this);

        var rowFont = TerminalFonts.Bold(_cache, 14);
        var small = TerminalFonts.Bold(_cache, 11);

        StyleClasses.Add(StyleNano.StyleClassPanelTerminalBlackM);
        MinSize = new Vector2(270, 0);
        MouseFilter = MouseFilterMode.Pass;

        _title = new Label
        {
            Text = ">> " + Loc.GetString("blackm-hub-title"),
            FontOverride = TerminalFonts.Bold(_cache, 15),
            FontColorOverride = StyleNano.TerminalGreen,
            HorizontalAlignment = HAlignment.Left,
        };

        var list = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            MinSize = new Vector2(252, 0),
        };
        list.AddChild(new Label
        {
            Text = Loc.GetString("blackm-hub-catalog"),
            FontOverride = small,
            FontColorOverride = StyleNano.TerminalGreen.WithAlpha(0.6f),
            Margin = new Thickness(2, 0, 0, 4),
        });

        AddEntry(list, rowFont, small, 1, "humanoid-profile-editor-appearance-tab");
        AddEntry(list, rowFont, small, 2, "humanoid-profile-editor-jobs-tab");
        AddEntry(list, rowFont, small, 3, "humanoid-profile-editor-antags-tab");
        AddEntry(list, rowFont, small, 4, "humanoid-profile-editor-traits-tab");
        AddEntry(list, rowFont, small, 5, "humanoid-profile-editor-markings-tab");
        AddEntry(list, rowFont, small, 6, null);

        _utilities = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
        };

        AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 5,
            Margin = new Thickness(8),
            Children =
            {
                _title,
                new PanelContainer
                {
                    MinSize = new Vector2(0, 1),
                    PanelOverride = new StyleBoxFlat { BackgroundColor = StyleNano.TerminalGreenDim },
                },
                list,
                new PanelContainer
                {
                    MinSize = new Vector2(0, 1),
                    PanelOverride = new StyleBoxFlat { BackgroundColor = StyleNano.TerminalGreenDim },
                },
                _utilities,
            },
        });
    }

    public void AddUtility(Control control)
    {
        _utilities.AddChild(control);
    }

    private void AddEntry(BoxContainer list, Font rowFont, Font small, int number, string? tabId)
    {
        var title = (tabId != null ? Loc.GetString(tabId) : Loc.GetString("blackm-hub-setup-title"))
            .ToUpperInvariant();

        var marker = new Label
        {
            Text = ">",
            FontOverride = rowFont,
            FontColorOverride = StyleNano.TerminalGreen,
            MinSize = new Vector2(16, 0),
            Modulate = new Color(1f, 1f, 1f, 0f),
            VerticalAlignment = VAlignment.Center,
            MouseFilter = MouseFilterMode.Ignore,
        };

        var row = new ContainerButton
        {
            MinSize = new Vector2(252, 30),
            HorizontalExpand = true,
            StyleClasses = { StyleNano.StyleClassJobButtonTerminalBlackM },
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    SeparationOverride = 6,
                    MouseFilter = MouseFilterMode.Ignore,
                    Children =
                    {
                        marker,
                        new Label
                        {
                            Text = $"{number:00}",
                            FontOverride = small,
                            FontColorOverride = StyleNano.TerminalGreen.WithAlpha(0.5f),
                            MinSize = new Vector2(20, 0),
                            VerticalAlignment = VAlignment.Center,
                            MouseFilter = MouseFilterMode.Ignore,
                        },
                        new Label
                        {
                            Text = title,
                            FontOverride = rowFont,
                            FontColorOverride = StyleNano.TerminalGreen,
                            ClipText = true,
                            HorizontalExpand = true,
                            VerticalAlignment = VAlignment.Center,
                            MouseFilter = MouseFilterMode.Ignore,
                        },
                    },
                },
            },
        };

        var entry = new Entry { TabId = tabId, Row = row, Marker = marker };
        var index = _entries.Count;

        row.OnMouseEntered += _ => Select(index);
        row.OnPressed += _ => Activated?.Invoke(entry.TabId);

        _entries.Add(entry);
        list.AddChild(row);
    }

    private void Select(int index)
    {
        if (index == _selected)
            return;

        _selected = index;

        for (var i = 0; i < _entries.Count; i++)
        {
            var selected = i == index;
            _entries[i].Marker.Modulate = new Color(1f, 1f, 1f, selected ? 1f : 0f);
            _entries[i].Row.Pressed = selected;
        }

    }

    public void HoldHidden()
    {
        Visible = false;
    }

    public void PlayIntro()
    {
        Visible = true;
        if (_selected < 0)
            Select(0);
    }

}
