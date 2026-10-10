using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Robust.Client.UserInterface.StylesheetHelpers;

namespace Content.Client._BlackM.Cargo.UI;

/// <summary>Local terminal styling; does not modify other cargo or station interfaces.</summary>
internal static class CargoTerminalStylesheet
{
    public static Stylesheet Create(IResourceCache resources)
    {
        var green = StyleNano.TerminalGreen;
        var dim = StyleNano.TerminalGreenDim;
        var black = StyleNano.TerminalBlack;
        var font = new VectorFont(resources.GetResource<FontResource>("/Fonts/RobotoMono/RobotoMono-Regular.ttf"), 12);
        var heading = new VectorFont(resources.GetResource<FontResource>("/Fonts/RobotoMono/RobotoMono-Bold.ttf"), 14);
        var normal = Box(black.WithAlpha(0.5f), dim);
        var hover = Box(dim, green);
        var pressed = Box(green.WithAlpha(0.90f), green);
        var disabled = Box(black, dim.WithAlpha(0.5f));

        return new Stylesheet(new StyleRule[]
        {
            Element<Label>().Prop("font", font).Prop("font-color", green),
            Element<Label>().Class("TerminalHeading").Prop("font", heading),
            Element<Label>().Class("TerminalMuted").Prop("font-color", green.WithAlpha(0.60f)),
            Element<RichTextLabel>().Prop("font", font).Prop("font-color", green),
            Element<CargoTerminalListButton>().Prop("stylebox", new StyleBoxFlat
            {
                BackgroundColor = black.WithAlpha(0.20f), BorderColor = dim.WithAlpha(0.65f),
                BorderThickness = new Thickness(0, 0, 0, 1), ContentMarginLeftOverride = 6, ContentMarginRightOverride = 6,
            }),
            Element<CargoTerminalListButton>().Pseudo("hover").Prop("stylebox", hover),
            Element<CargoTerminalListButton>().Pseudo("pressed").Prop("stylebox", pressed),
            Element<TextureRect>().Class(OptionButton.StyleClassOptionTriangle)
                .Prop(TextureRect.StylePropertyTexture, resources.GetResource<TextureResource>("/Textures/Interface/Nano/inverted_triangle.svg.png").Texture)
                .Prop(Control.StylePropertyModulateSelf, green),
            Element<PanelContainer>().Class("TerminalFrame").Prop("panel", Box(black, dim, 0)),
            Element<PanelContainer>().Class("TerminalScreen").Prop("panel", new StyleBoxFlat(black)),
            Element<PanelContainer>().Class("TerminalRule").Prop("panel", new StyleBoxFlat(dim)),
            Element<ContainerButton>().Prop("stylebox", normal),
            Element<ContainerButton>().Pseudo("hover").Prop("stylebox", hover),
            Element<ContainerButton>().Pseudo("pressed").Prop("stylebox", pressed),
            Element<ContainerButton>().Pseudo("disabled").Prop("stylebox", disabled),
            Child().Parent(Element<ContainerButton>().Pseudo("pressed")).Child(Element<Label>()).Prop("font-color", black),
            Child().Parent(Element<ContainerButton>().Pseudo("disabled")).Child(Element<Label>()).Prop("font-color", dim),
            Element<LineEdit>().Prop("font", font).Prop("font-color", green)
                .Prop("stylebox", normal).Prop("cursor-color", green).Prop("selection-color", dim),
            Element<LineEdit>().Pseudo("placeholder").Prop("font-color", Color.LightGray),
            Element<ScrollBar>().Prop("grabber", Box(dim, dim, 0)),
            Element<ScrollBar>().Pseudo("hover").Prop("grabber", Box(green, green, 0)),
        });
    }

    private static StyleBoxFlat Box(Color background, Color border, int padding = 5)
    {
        return new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border,
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = padding,
            ContentMarginRightOverride = padding,
            ContentMarginTopOverride = 4,
            ContentMarginBottomOverride = 4,
        };
    }
}
