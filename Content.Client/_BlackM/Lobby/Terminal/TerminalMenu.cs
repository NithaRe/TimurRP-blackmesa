using System.Numerics;
using System.Linq;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client._BlackM.Lobby.Terminal;

public sealed class TerminalMenu : BoxContainer
{
    [Dependency] private readonly IResourceCache _cache = default!;

    private sealed class Entry
    {
        public Button Button = default!;
        public Label Marker = default!;
    }

    private readonly List<Entry> _entries = new();
    private Label? _category;
    private int _selected = -1;

    public TerminalMenu()
    {
        IoCManager.InjectDependencies(this);
        _category = new Label
        {
            Text = "[ ПРОЧЕЕ ]",
            FontOverride = TerminalFonts.Bold(_cache, 11),
            FontColorOverride = StyleNano.TerminalGreen.WithAlpha(0.6f),
            Margin = new Thickness(2, 5, 0, 2),
        };
        AddChild(_category);
    }

    public void ConfigureOtherEntries(params (Button Button, string Title, string? IconPath)[] entries)
    {
        foreach (var (button, title, iconPath) in entries)
        {
            if (_entries.Any(entry => entry.Button == button))
                continue;

            button.RemoveStyleClass(ContainerButton.StyleClassButton);
            button.AddStyleClass(StyleNano.StyleClassJobButtonTerminalBlackM);
            button.MinSize = new Vector2(252, 30);
            button.HorizontalExpand = true;
            button.ToggleMode = true;
            button.Label.Visible = false;

            foreach (var child in button.Children.ToArray())
            {
                if (child != button.Label)
                    button.RemoveChild(child);
            }

            var marker = new Label
            {
                Text = ">",
                FontOverride = TerminalFonts.Bold(_cache, 14),
                FontColorOverride = StyleNano.TerminalGreen,
                MinSize = new Vector2(16, 0),
                Modulate = new Color(1f, 1f, 1f, 0f),
                VerticalAlignment = VAlignment.Center,
                MouseFilter = MouseFilterMode.Ignore,
            };

            var row = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                SeparationOverride = 6,
                HorizontalExpand = true,
                MouseFilter = MouseFilterMode.Ignore,
            };
            row.AddChild(marker);
            row.AddChild(new Label
            {
                Text = $"{_entries.Count + 7:00}",
                FontOverride = TerminalFonts.Bold(_cache, 11),
                FontColorOverride = StyleNano.TerminalGreen.WithAlpha(0.5f),
                MinSize = new Vector2(20, 0),
                VerticalAlignment = VAlignment.Center,
                MouseFilter = MouseFilterMode.Ignore,
            });
            if (iconPath != null)
            {
                row.AddChild(new TextureRect
                {
                    TexturePath = iconPath,
                    MinSize = new Vector2(18, 18),
                    Stretch = TextureRect.StretchMode.KeepAspectCentered,
                    VerticalAlignment = VAlignment.Center,
                    MouseFilter = MouseFilterMode.Ignore,
                });
            }
            row.AddChild(new Label
            {
                Text = title.ToUpperInvariant(),
                FontOverride = TerminalFonts.Bold(_cache, 14),
                FontColorOverride = StyleNano.TerminalGreen,
                ClipText = true,
                HorizontalExpand = true,
                VerticalAlignment = VAlignment.Center,
                MouseFilter = MouseFilterMode.Ignore,
            });
            button.AddChild(row);

            var entry = new Entry { Button = button, Marker = marker };
            var index = _entries.Count;
            button.OnMouseEntered += _ => Select(index);
            _entries.Add(entry);
        }

        RefreshCatalogStyle();
    }

    public void RefreshCatalogStyle()
    {
        for (var i = 0; i < _entries.Count; i++)
        {
            var selected = i == _selected;
            _entries[i].Marker.Modulate = new Color(1f, 1f, 1f, selected ? 1f : 0f);
            _entries[i].Button.Pressed = selected;
        }
    }

    private void Select(int index)
    {
        if (index == _selected)
            return;

        _selected = index;
        RefreshCatalogStyle();
    }

    public void PlayIntro()
    {
        Visible = true;
        if (_selected < 0 && _entries.Count > 0)
            Select(0);
    }
}
