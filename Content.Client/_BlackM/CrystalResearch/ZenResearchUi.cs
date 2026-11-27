using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Robust.Client.UserInterface.StylesheetHelpers;

namespace Content.Client._BlackM.CrystalResearch;

internal static class ZenResearchUi
{
    public static readonly Color Amber = Color.FromHex("#D7AE66");
    public static readonly Color Green = Color.FromHex("#81B79A");
    public static readonly Color Muted = Color.FromHex("#A1A4A6");
    public static readonly Color Red = Color.FromHex("#DB7165");

    public static BoxContainer Column(int separation = 8) => new()
    {
        Orientation = BoxContainer.LayoutOrientation.Vertical,
        HorizontalExpand = true,
        SeparationOverride = separation,
    };

    public static RichTextLabel Text(string value = "")
    {
        var label = new RichTextLabel { HorizontalExpand = true };
        label.SetMessage(value);
        return label;
    }

    public static PanelContainer Screen(Control child)
    {
        var panel = new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            PanelOverride = new StyleBoxFlat(Color.FromHex("#171A1B"))
            {
                ContentMarginLeftOverride = 12,
                ContentMarginRightOverride = 12,
                ContentMarginTopOverride = 10,
                ContentMarginBottomOverride = 10,
            },
        };
        panel.AddChild(child);
        return panel;
    }

    public static ScrollContainer Scroll(Control child)
    {
        var scroll = new ScrollContainer { HScrollEnabled = false, HorizontalExpand = true, VerticalExpand = true };
        scroll.AddChild(child);
        return scroll;
    }

    public static PanelContainer Rule() => new()
    {
        MinHeight = 1,
        MaxHeight = 1,
        HorizontalExpand = true,
        PanelOverride = new StyleBoxFlat(Color.FromHex("#3B3E3E")),
    };

    public static ProgressBar Gauge(Color color) => new()
    {
        MinValue = 0,
        MaxValue = 1,
        MinHeight = 8,
        MaxHeight = 8,
        HorizontalExpand = true,
        BackgroundStyleBoxOverride = new StyleBoxFlat(Color.FromHex("#303436")),
        ForegroundStyleBoxOverride = new StyleBoxFlat(color),
    };

    public static Stylesheet Styles(IResourceCache resources)
    {
        var font = new VectorFont(resources.GetResource<FontResource>("/Fonts/RobotoMono/RobotoMono-Regular.ttf"), 12);
        var regular = new StyleBoxFlat(Color.FromHex("#252B2D"))
        {
            BorderColor = Color.FromHex("#505855"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            ContentMarginLeftOverride = 8,
            ContentMarginRightOverride = 8,
            ContentMarginTopOverride = 6,
            ContentMarginBottomOverride = 6,
        };
        var active = new StyleBoxFlat(Color.FromHex("#3D382B"))
        {
            BorderColor = Amber,
            BorderThickness = new Thickness(2, 0, 0, 0),
            ContentMarginLeftOverride = 8,
            ContentMarginRightOverride = 8,
            ContentMarginTopOverride = 6,
            ContentMarginBottomOverride = 6,
        };
        return new Stylesheet(new StyleRule[]
        {
            Element<Label>().Prop("font", font).Prop("font-color", Color.FromHex("#E1E1DA")),
            Element<RichTextLabel>().Prop("font", font).Prop("font-color", Color.FromHex("#CDD1CA")),
            Element<Button>().Prop("stylebox", regular),
            Element<Button>().Pseudo("hover").Prop("stylebox", active),
            Element<Button>().Pseudo("pressed").Prop("stylebox", active),
            Element<Button>().Pseudo("disabled").Prop("modulate-self", Color.FromHex("#777777")),
        });
    }
}
