using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using static Robust.Client.UserInterface.StylesheetHelpers;

namespace Content.Client._BlackM.Underground;

internal static class UndergroundConsoleStylesheet
{
    public static readonly Color Ink = Color.FromHex("#e8e4d5");
    public static readonly Color Muted = Color.FromHex("#aaa9a3");
    public static readonly Color Amber = Color.FromHex("#ebbb63");
    public static readonly Color Blue = Color.FromHex("#c8c7c0");

    public static Stylesheet Create(IResourceCache resources)
    {
        var body = new VectorFont(resources.GetResource<FontResource>("/Fonts/_BlackM/Tektur-Regular.ttf"), 12);
        var heading = new VectorFont(resources.GetResource<FontResource>("/Fonts/_BlackM/Tektur-Bold.ttf"), 16);
        return new Stylesheet(new StyleRule[]
        {
            Element<Label>().Prop("font", body).Prop("font-color", Ink),
            Element<Label>().Class("UndergroundHeading").Prop("font", heading),
            Element<RichTextLabel>().Prop("font", body).Prop("font-color", Ink),
            Element<RichTextLabel>().Class("UndergroundHeading").Prop("font", heading),
            Element<ContainerButton>().Class("ContractCard").Prop("stylebox", Box("#292929", "#494949", 8)),
            Element<ContainerButton>().Class("ContractCard").Pseudo("hover").Prop("stylebox", Box("#3a3a3a", "#c8c7c0", 8)),
            Element<ContainerButton>().Class("ContractCard").Pseudo("pressed").Prop("stylebox", Box("#3c382e", "#ebbb63", 8)),
            Element<ContainerButton>().Class("ContractCard").Pseudo("disabled").Prop("stylebox", Box("#292929", "#494949", 8)),
            Element<ContainerButton>().Class("PrimaryAction").Prop("stylebox", Box("#876034", "#e5b866", 9)),
            Element<ContainerButton>().Class("PrimaryAction").Pseudo("hover").Prop("stylebox", Box("#a8793e", "#f5d193", 9)),
            Element<ContainerButton>().Class("PrimaryAction").Pseudo("pressed").Prop("stylebox", Box("#b78745", "#f5d193", 9)),
            Element<ContainerButton>().Class("PrimaryAction").Pseudo("disabled").Prop("stylebox", Box("#303030", "#505050", 9)),
            Element<ContainerButton>().Class("SecondaryAction").Prop("stylebox", Box("#333333", "#777777", 9)),
            Element<ContainerButton>().Class("SecondaryAction").Pseudo("hover").Prop("stylebox", Box("#474747", "#aaaaaa", 9)),
            Element<ContainerButton>().Class("SecondaryAction").Pseudo("pressed").Prop("stylebox", Box("#555555", "#aaaaaa", 9)),
            Element<ContainerButton>().Class("SecondaryAction").Pseudo("disabled").Prop("stylebox", Box("#303030", "#505050", 9)),
        });
    }

    private static StyleBoxFlat Box(string background, string border, int padding) => new()
    {
        BackgroundColor = Color.FromHex(background),
        BorderColor = Color.FromHex(border),
        BorderThickness = new Thickness(1),
        ContentMarginLeftOverride = padding,
        ContentMarginRightOverride = padding,
        ContentMarginTopOverride = 5,
        ContentMarginBottomOverride = 5,
    };
}
