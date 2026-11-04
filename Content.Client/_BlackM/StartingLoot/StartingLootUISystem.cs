using System.Threading;
using Content.Shared._BlackM.StartingLoot;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.ContentPack;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Client._BlackM.StartingLoot;

public sealed class StartingLootUISystem : EntitySystem
{
    private const string BagFallSound = "/Audio/Effects/holy.ogg";
    private const string BagLandSound = "/Audio/Effects/hit_kick.ogg";
    private const string BagClickSound = "/Audio/Effects/rustle1.ogg";
    private const string DiceRollSound = "/Audio/Items/Dice/dice1.ogg";

    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IResourceManager _res = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private StartingLootPanel? _panel;
    private bool _pending;
    private CancellationTokenSource? _timerCancelToken;

    public override void Initialize()
    {
        SubscribeNetworkEvent<StartingLootRolledEvent>(OnRolled);
    }

    private void OnRolled(StartingLootRolledEvent ev)
    {
        if (_pending || _panel?.Parent != null)
            return;

        _pending = true;

        _timerCancelToken?.Cancel();
        _timerCancelToken?.Dispose();
        _timerCancelToken = new CancellationTokenSource();
        var token = _timerCancelToken.Token;

        Timer.Spawn(1500, () =>
        {
            _pending = false;

            if (token.IsCancellationRequested)
                return;

            _panel?.Close();
            _panel = new StartingLootPanel(ev.Results);
            _panel.BagDropped += () => Play(BagFallSound);
            _panel.BagLanded += () => Play(BagLandSound);
            _panel.BagClicked += () => Play(BagClickSound);
            _panel.DiceThrown += () => Play(DiceRollSound);

            Control parent = _ui.ActiveScreen ?? (Control) _ui.WindowRoot;
            LayoutContainer.SetAnchorPreset(_panel, LayoutContainer.LayoutPreset.Wide);
            _panel.HorizontalExpand = true;
            _panel.VerticalExpand = true;
            parent.AddChild(_panel);
        }, token);
    }

    private void Play(string path)
    {
        if (!_res.ContentFileExists(new ResPath(path)))
            return;

        _audio.PlayGlobal(path, Filter.Local(), false, AudioParams.Default.WithVolume(-3f));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _timerCancelToken?.Cancel();
        _timerCancelToken?.Dispose();
        _timerCancelToken = null;
        _pending = false;

        _panel?.Close();
        _panel = null;
    }
}