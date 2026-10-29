using System.Numerics;
using Content.Client.Stylesheets;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._BlackM.Lobby.Terminal;

public sealed class TerminalHub : BoxContainer
{
    [Dependency] private readonly IResourceCache _cache = default!;

    public event Action<string?>? Activated;

    public TerminalHub()
    {
        IoCManager.InjectDependencies(this);

        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 6;
        HorizontalExpand = true;
        MouseFilter = MouseFilterMode.Pass;

        AddChild(new Label
        {
            Text = Loc.GetString("blackm-hub-catalog"),
            FontOverride = TerminalFonts.Bold(_cache, 11),
            FontColorOverride = StyleNano.TerminalGreen.WithAlpha(0.6f),
            Margin = new Thickness(2, 0, 0, 0),
        });

        var top = NewRow();
        AddEntry(top, "humanoid-profile-editor-appearance-tab");
        AddEntry(top, "humanoid-profile-editor-jobs-tab");

        var bottom = NewRow();
        AddEntry(bottom, "humanoid-profile-editor-traits-tab");
        AddEntry(bottom, "humanoid-profile-editor-markings-tab");
    }

    private BoxContainer NewRow()
    {
        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };
        AddChild(row);
        return row;
    }

    private void AddEntry(BoxContainer row, string tabId)
    {
        var button = new ContainerButton
        {
            MinSize = new Vector2(0, 38),
            HorizontalExpand = true,
            StyleClasses = { StyleNano.StyleClassFlatButtonTerminalBlackM },
        };

        button.AddChild(new Label
        {
            Text = Loc.GetString(tabId).ToUpperInvariant(),
            FontOverride = TerminalFonts.Bold(_cache, 13),
            FontColorOverride = StyleNano.TerminalGreen,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            MouseFilter = MouseFilterMode.Ignore,
        });

        button.OnPressed += _ => Activated?.Invoke(tabId);
        row.AddChild(button);
    }

    public void HoldHidden()
    {
        Visible = false;
    }

    public void PlayIntro()
    {
        Visible = true;
    }
}
