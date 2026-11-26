using Robust.Shared.Audio;

namespace Content.Shared._BlackM.SecurityCommand;

public static class SecurityCommandSounds
{
    private static readonly SoundSpecifier TerminalSelect =
        new SoundPathSpecifier("/Audio/_BlackM/Interface/Terminal/term_select.ogg");
    private static readonly SoundSpecifier TerminalConfirm =
        new SoundPathSpecifier("/Audio/_BlackM/Interface/Terminal/term_confirm.ogg");
    private static readonly SoundSpecifier RadioEnd =
        new SoundPathSpecifier("/Audio/_BlackM/Radio/send_end_1.ogg");
    private static readonly SoundSpecifier ShortWarning =
        new SoundPathSpecifier("/Audio/Machines/Nuke/angry_beep.ogg");

    public static readonly SoundSpecifier UiToggle = TerminalSelect;

    public static readonly SoundSpecifier ToolSelect = TerminalSelect;

    public static readonly SoundSpecifier PointPlaced =
        new SoundPathSpecifier("/Audio/_BlackM/Interface/Terminal/term_click.ogg");

    public static readonly SoundSpecifier OrderSent = TerminalConfirm;

    public static readonly SoundSpecifier PlacementCancel = RadioEnd;

    public static readonly SoundSpecifier Invalid = ShortWarning;

    public static readonly SoundSpecifier OrderRemoved = RadioEnd;

    public static readonly SoundSpecifier OrdersCleared = RadioEnd;

    public static readonly SoundSpecifier SquadCreated = TerminalConfirm;

    public static readonly SoundSpecifier OfficerAssigned = TerminalConfirm;

    public static readonly SoundSpecifier Arrival = TerminalConfirm;

    public static readonly SoundSpecifier NewOrder = new SoundPathSpecifier("/Audio/_BlackM/Radio/receive_1.ogg");

    public static readonly SoundSpecifier DangerOrder =
        new SoundPathSpecifier("/Audio/_BlackM/Announcements/terminal_alert.ogg");

    public static readonly SoundSpecifier OrderCancelled = RadioEnd;

    public static readonly SoundSpecifier Assigned = new SoundPathSpecifier("/Audio/_BlackM/Radio/receive_2.ogg");

    public static readonly SoundSpecifier Unassigned = RadioEnd;

    public static readonly SoundSpecifier PostInterrupted = ShortWarning;

    public static readonly SoundSpecifier OfficerDown =
        new SoundPathSpecifier("/Audio/_Mono/Effects/Computer/beep_series2.ogg");
}
