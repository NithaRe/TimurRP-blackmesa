using System.Diagnostics;
using Robust.Client.Audio;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Audio;
using Robust.Shared.Input;
using Robust.Shared.Player;

namespace Content.Client._BlackM.Lobby.Terminal;

public static class TerminalSounds
{
    public const string Root = "/Audio/_BlackM/Interface/Terminal/";

    public const string Hover = Root + "term_hover.ogg";
    public const string Click = Root + "term_click.ogg";
    public const string Confirm = Root + "term_confirm.ogg";
    public const string ToggleOn = Root + "term_toggle_on.ogg";
    public const string ToggleOff = Root + "term_toggle_off.ogg";
    public const string Error = Root + "term_error.ogg";
    public const string Tick = Root + "term_type.ogg";
    public const string Select = Root + "term_select.ogg";
    public const string Open = Root + "term_open.ogg";
    public const string Close = Root + "term_close.ogg";
    public const string Boot = Root + "term_boot.ogg";

    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly Dictionary<string, long> LastPlayed = new();
    private const string AttachedMarker = "BlackMTerminalSounds";

    public static void Play(string path, float volume = -4f, float variation = 0.04f, int minIntervalMs = 25)
    {
        var now = Clock.ElapsedMilliseconds;
        if (minIntervalMs > 0 && LastPlayed.TryGetValue(path, out var last) && now - last < minIntervalMs)
            return;
        LastPlayed[path] = now;

        var sysMan = IoCManager.Resolve<IEntitySystemManager>();
        if (!sysMan.TryGetEntitySystem<AudioSystem>(out var audio))
            return;

        audio.PlayGlobal(path, Filter.Local(), false,
            AudioParams.Default.WithVolume(volume).WithVariation(variation));
    }

    public static void Attach(BaseButton button)
    {
        if (button.HasStyleClass(AttachedMarker))
            return;
        button.AddStyleClass(AttachedMarker);

        button.OnMouseEntered += _ =>
        {
            if (!button.Disabled)
                Play(Hover, -10f, 0.06f, 40);
        };

        button.OnPressed += _ =>
        {
            if (!button.ToggleMode)
                Play(Click);
        };

        button.OnToggled += args => Play(args.Pressed ? ToggleOn : ToggleOff);

        button.OnKeyBindDown += args =>
        {
            if (button.Disabled && args.Function == EngineKeyFunctions.UIClick)
                Play(Error, -6f, 0.02f, 120);
        };
    }

    public static void AttachAll(Control root)
    {
        if (root is BaseButton button)
            Attach(button);

        foreach (var child in root.Children)
            AttachAll(child);
    }
}
