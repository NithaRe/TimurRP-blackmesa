using System.Threading;
using Content.Shared._BlackM.DiscordAuth;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Shared.Network;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Client._BlackM.DiscordAuth;

public sealed partial class DiscordAuthState : State
{
    [Dependency] private IUserInterfaceManager _userInterfaceManager = default!;
    [Dependency] private IClientNetManager _netManager = default!;

    private DiscordAuthGui? _gui;
    private readonly CancellationTokenSource _checkTimerCancel = new();

    protected override void Startup()
    {
        _gui = new DiscordAuthGui();
        _userInterfaceManager.StateRoot.AddChild(_gui);

        SendCheck();

        Timer.SpawnRepeating(TimeSpan.FromSeconds(5), SendCheck, _checkTimerCancel.Token);
    }

    private void SendCheck()
    {
        if (_netManager.IsConnected)
            _netManager.ClientSendMessage(new MsgDiscordAuthCheck());
    }

    protected override void Shutdown()
    {
        _checkTimerCancel.Cancel();
        _gui?.Dispose();
        _gui = null;
    }
}
