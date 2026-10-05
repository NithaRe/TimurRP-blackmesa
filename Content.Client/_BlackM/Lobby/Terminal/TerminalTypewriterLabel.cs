using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.Lobby.Terminal;

public sealed class TerminalTypewriterLabel : Label
{
    private const string CursorChar = "_";

    private string _full = string.Empty;
    private float _progress;
    private float _blink;
    private bool _typing;
    private int _lastShown = -1;
    private bool _lastCursor;
    private int _lastTickAt;

    public float CharsPerSecond { get; set; } = 70f;
    public bool TickSound { get; set; } = true;
    public bool CursorEnabled { get; set; } = true;
    public bool IsTyping => _typing;

    public event Action? Finished;

    public void Reveal(string text, float? charsPerSecond = null)
    {
        _full = text;
        _progress = 0f;
        _typing = true;
        _lastShown = -1;
        _lastTickAt = 0;

        if (charsPerSecond != null)
            CharsPerSecond = charsPerSecond.Value;

        Refresh(0, true);
    }

    public void Skip()
    {
        if (_typing)
            _progress = _full.Length;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_full.Length == 0 && !_typing)
            return;

        _blink += Math.Min(args.DeltaSeconds, 0.05f);
        var cursorOn = CursorEnabled && ((int) (_blink * 2f) % 2 == 0);

        if (!_typing)
        {
            Refresh(_full.Length, cursorOn);
            return;
        }

        _progress += Math.Min(args.DeltaSeconds, 0.05f) * CharsPerSecond;
        var shown = Math.Min((int) _progress, _full.Length);

        if (shown >= _full.Length)
        {
            _typing = false;
            Refresh(_full.Length, cursorOn);
            Finished?.Invoke();
            return;
        }

        if (TickSound && shown - _lastTickAt >= 3)
        {
            _lastTickAt = shown;
            TerminalSounds.Play(TerminalSounds.Tick, -16f, 0.1f, 30);
        }

        Refresh(shown, true);
    }

    private void Refresh(int shown, bool cursor)
    {
        if (shown == _lastShown && cursor == _lastCursor)
            return;

        _lastShown = shown;
        _lastCursor = cursor;

        var visible = _full[..shown];
        Text = CursorEnabled ? visible + (cursor ? CursorChar : " ") : visible;
    }
}
