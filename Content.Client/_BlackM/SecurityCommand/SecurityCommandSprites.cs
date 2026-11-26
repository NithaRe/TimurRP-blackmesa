using System.Collections.Generic;
using Content.Shared._BlackM.SecurityCommand;
using Robust.Shared.Utility;

namespace Content.Client._BlackM.SecurityCommand;

public static class SecurityCommandSprites
{
    private static readonly ResPath JobIcons = new("/Textures/_BlackM/Interface/Misc/job_icons.rsi");
    private static readonly ResPath HealthIcons = new("/Textures/Interface/Misc/health_icons.rsi");

    public const bool OfficerIconsTintable = false;

    public static readonly SpriteSpecifier Waypoint = Verb("dot");
    public static readonly SpriteSpecifier Officer = new SpriteSpecifier.Rsi(JobIcons, "SecurityOfficer");
    public static readonly SpriteSpecifier Commander = new SpriteSpecifier.Rsi(JobIcons, "HeadOfSecurity");
    public static readonly SpriteSpecifier OfficerCritical = new SpriteSpecifier.Rsi(HealthIcons, "Critical");
    public static readonly SpriteSpecifier OfficerDead = new SpriteSpecifier.Rsi(HealthIcons, "Dead");
    public static readonly SpriteSpecifier Command =
        new SpriteSpecifier.Rsi(new ResPath("/Textures/Clothing/Head/Hats/beret_hos.rsi"), "icon");

    public static readonly SpriteSpecifier UiSidebar = Verb("group");
    public static readonly SpriteSpecifier UiRefresh = Verb("refresh");
    public static readonly SpriteSpecifier UiCenter = Verb("dot");
    public static readonly SpriteSpecifier UiShow = Verb("examine");
    public static readonly SpriteSpecifier UiTarget = Verb("point");
    public static readonly SpriteSpecifier UiPlus = Verb("plus");
    public static readonly SpriteSpecifier UiDelete = Verb("delete");
    public static readonly SpriteSpecifier UiClose = Verb("close");
    public static readonly SpriteSpecifier UiFinish =
        new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Nano/checkbox_checked.svg.96dpi.png"));

    public static SpriteSpecifier Order(SecurityOrderType type)
    {
        return type switch
        {
            SecurityOrderType.Move => new SpriteSpecifier.Rsi(SecurityCommandPixel.MarkersRsi, "pointer"),
            SecurityOrderType.Post => new SpriteSpecifier.Rsi(SecurityCommandPixel.MarkersRsi, "post"),
            SecurityOrderType.PatrolRoute => new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Nano/circular_arrow.svg.96dpi.png")),
            SecurityOrderType.PatrolZone => new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Nano/Monotone/monotone_checkbox_unchecked.svg.96dpi.png")),
            SecurityOrderType.Danger => new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/NavMap/beveled_triangle.png")),
            _ => new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/pencil.png")),
        };
    }

    private static readonly HashSet<string> JobStates = new()
    {
        "HeadOfSecurity",
        "Warden",
        "SecurityOfficer",
        "SecurityCadet",
        "Brigmedic",
        "Captain",
    };

    public static SpriteSpecifier ForOfficer(SecurityOfficerData officer)
    {
        return officer.Status switch
        {
            SecurityOfficerStatus.Dead => OfficerDead,
            SecurityOfficerStatus.Critical => OfficerCritical,
            _ => ForJob(officer),
        };
    }

    public static SpriteSpecifier ForJob(SecurityOfficerData officer)
    {
        if (!string.IsNullOrEmpty(officer.JobId) && JobStates.Contains(officer.JobId))
            return new SpriteSpecifier.Rsi(JobIcons, officer.JobId);

        return officer.IsCommander ? Commander : Officer;
    }

    private static SpriteSpecifier.Texture Verb(string name)
    {
        return new SpriteSpecifier.Texture(new ResPath($"/Textures/Interface/VerbIcons/{name}.svg.192dpi.png"));
    }
}
