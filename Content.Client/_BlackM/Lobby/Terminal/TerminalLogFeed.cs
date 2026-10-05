using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using Content.Client.Stylesheets;

namespace Content.Client._BlackM.Lobby.Terminal;

public sealed class TerminalLogFeed : Label
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IResourceCache _cache = default!;

    private const int MaxLines = 9;
    private const int MessageCount = 10;

    private readonly Queue<string> _lines = new();
    private readonly Random _rng = new();
    private float _next = 0.4f;

    public TerminalLogFeed()
    {
        IoCManager.InjectDependencies(this);

        FontOverride = TerminalFonts.Bold(_cache, 10);
        FontColorOverride = StyleNano.TerminalGreen.WithAlpha(0.55f);
        MouseFilter = MouseFilterMode.Ignore;
        MinSize = new System.Numerics.Vector2(520, 0);

        for (var i = 0; i < 4; i++)
            AddLine();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        _next -= Math.Min(args.DeltaSeconds, 0.05f);
        if (_next > 0f)
            return;

        _next = 0.7f + _rng.NextSingle() * 1.9f;
        AddLine();
    }

    private void AddLine()
    {
        var stamp = _timing.RealTime.ToString(@"hh\:mm\:ss");
        var message = Loc.GetString($"blackm-log-{_rng.Next(1, MessageCount + 1)}");

        _lines.Enqueue($"[{stamp}] {message}");
        while (_lines.Count > MaxLines)
            _lines.Dequeue();

        Text = string.Join("\n", _lines);
    }
}
