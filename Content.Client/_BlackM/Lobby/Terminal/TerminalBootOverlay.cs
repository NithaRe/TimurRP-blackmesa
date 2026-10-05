using System.Text;
using Content.Client.Resources;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.Lobby.Terminal;

public sealed class TerminalBootOverlay : PanelContainer
{
    private static bool _shown;

    private const float StartDelay = 0.6f;
    private const float HoldTime = 1.0f;
    private const float FadeTime = 0.5f;

    private readonly TerminalTypewriterLabel _label;
    private bool _playing;
    private bool _started;
    private float _delay = StartDelay;
    private float _hold;
    private float _fade;

    public static bool WillPlay => !_shown;

    public event Action? Finished;

    public TerminalBootOverlay()
    {
        MouseFilter = MouseFilterMode.Stop;
        PanelOverride = new StyleBoxFlat { BackgroundColor = StyleNano.TerminalBlack };

        var font = TerminalFonts.Bold(IoCManager.Resolve<IResourceCache>(), 15);

        _label = new TerminalTypewriterLabel
        {
            FontOverride = font,
            FontColorOverride = StyleNano.TerminalGreen,
            Margin = new Thickness(48),
            HorizontalAlignment = HAlignment.Left,
            VerticalAlignment = VAlignment.Top,
            CharsPerSecond = 70f,
        };

        AddChild(_label);
        AddChild(new TerminalScanlines());
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();

        if (_shown || _playing)
        {
            if (!_playing)
                Dismiss(false);
            return;
        }

        _shown = true;
        _playing = true;
        _started = false;
        _delay = StartDelay;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (!_playing || args.Function != EngineKeyFunctions.UIClick)
            return;

        if (!_started)
            _delay = 0f;
        else if (_label.IsTyping)
            _label.Skip();
        else
            _hold = HoldTime + 0.001f;

        args.Handle();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_playing)
            return;

        var dt = Math.Min(args.DeltaSeconds, 0.05f);

        if (!_started)
        {
            _delay -= dt;
            if (_delay > 0f)
                return;

            _started = true;
            TerminalSounds.Play(TerminalSounds.Boot, -4f, 0f, 0);
            _label.Reveal(BuildText());
            return;
        }

        if (_label.IsTyping)
            return;

        _hold += dt;
        if (_hold <= HoldTime)
            return;

        _fade += dt / FadeTime;
        Modulate = new Color(1f, 1f, 1f, Math.Max(0f, 1f - _fade));

        if (_fade >= 1f)
            Dismiss(true);
    }

    private void Dismiss(bool raise)
    {
        _playing = false;
        Visible = false;
        MouseFilter = MouseFilterMode.Ignore;

        if (raise)
            Finished?.Invoke();
    }

    private static string BuildText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("BLACK MESA // " + Loc.GetString("blackm-term-title"));
        sb.AppendLine(new string('=', 46));

        foreach (var key in new[]
                 {
                     "blackm-term-boot-uplink",
                     "blackm-term-boot-modules",
                     "blackm-term-boot-records",
                     "blackm-term-boot-clock",
                     "blackm-term-boot-lobby",
                     "blackm-term-boot-access",
                 })
        {
            sb.AppendLine("> " + (Loc.GetString(key) + " ").PadRight(40, '.') + " OK");
        }

        sb.AppendLine();
        sb.Append("> " + Loc.GetString("blackm-term-boot-ready"));
        return sb.ToString();
    }
}
