using System.Numerics;
using System.Linq;
using Content.Client.Stylesheets;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._BlackM.Lobby.Terminal;

public sealed class TerminalMenu : BoxContainer
{
    [Dependency] private readonly IResourceCache _cache = default!;

    private readonly HashSet<Button> _configured = new();

    public TerminalMenu()
    {
        IoCManager.InjectDependencies(this);
        Orientation = LayoutOrientation.Horizontal;
    }

    public void ConfigureOtherEntries(params (Button Button, string Title, string? IconPath)[] entries)
    {
        foreach (var (button, title, iconPath) in entries)
        {
            if (!_configured.Add(button))
                continue;

            button.RemoveStyleClass(ContainerButton.StyleClassButton);
            button.AddStyleClass(StyleNano.StyleClassFlatButtonTerminalBlackM);
            button.MinSize = new Vector2(112, 34);
            button.HorizontalExpand = true;
            button.ToggleMode = false;
            button.Label.Visible = false;

            foreach (var child in button.Children.ToArray())
            {
                if (child != button.Label)
                    button.RemoveChild(child);
            }

            var row = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                SeparationOverride = 6,
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
                MouseFilter = MouseFilterMode.Ignore,
            };

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
                FontOverride = TerminalFonts.Bold(_cache, 13),
                FontColorOverride = StyleNano.TerminalGreen,
                VerticalAlignment = VAlignment.Center,
                MouseFilter = MouseFilterMode.Ignore,
            });

            button.AddChild(row);
        }
    }

    public void PlayIntro()
    {
        Visible = true;
    }
}
