using System;
using System.IO;
using System.Threading;
using Content.Shared._BlackM.CCVar;
using Content.Shared._BlackM.DiscordAuth;
using Robust.Client.Graphics;
using Robust.Client.State;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Client._BlackM.DiscordAuth;

public sealed partial class DiscordAuthManager : Content.Corvax.Interfaces.Client.IClientDiscordAuthManager
{
    [Dependency] private IClientNetManager _netManager = default!;
    [Dependency] private IStateManager _stateManager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    public string AuthUrl { get; private set; } = string.Empty;
    public Texture? Qrcode { get; private set; }
    public bool IsVerified { get; private set; } = false;
    public bool IsOpt { get; private set; }
    public bool IsEnabled { get; private set; }

    public event Action? DataUpdated;

    private readonly CancellationTokenSource _watchdogCancel = new();

    public void Initialize()
    {
        _netManager.RegisterNetMessage<MsgDiscordAuthCheck>();
        _netManager.RegisterNetMessage<MsgDiscordAuthByPass>();
        _netManager.RegisterNetMessage<MsgDiscordAuthRequired>(OnDiscordAuthRequired);
        _netManager.RegisterNetMessage<MsgDiscordAuthVerified>(OnDiscordAuthVerified);

        _cfg.OnValueChanged(BlackMCVars.DiscordAuthIsOptional, v => IsOpt = v, true);
        _cfg.OnValueChanged(BlackMCVars.DiscordAuthEnabled, v => IsEnabled = v, true);

        Timer.SpawnRepeating(TimeSpan.FromMilliseconds(100), EnforceAuthState, _watchdogCancel.Token);
    }

    private void EnforceAuthState()
    {
        if (!IsEnabled || IsVerified || IsOpt)
            return;

        if (_stateManager.CurrentState is not DiscordAuthState)
        {
            _stateManager.RequestStateChange<DiscordAuthState>();
        }
    }

    private void OnDiscordAuthRequired(MsgDiscordAuthRequired message)
    {
        IsVerified = false;

        AuthUrl = message.AuthUrl;
        if (message.QrCode.Length > 0)
        {
            using var ms = new MemoryStream(message.QrCode);
            Qrcode = Texture.LoadFromPNGStream(ms);
        }

        if (_stateManager.CurrentState is not DiscordAuthState)
        {
            _stateManager.RequestStateChange<DiscordAuthState>();
        }

        DataUpdated?.Invoke();
    }

    private void OnDiscordAuthVerified(MsgDiscordAuthVerified message)
    {
        IsVerified = true;

        if (_stateManager.CurrentState is DiscordAuthState)
        {
            _stateManager.RequestStateChange<Content.Client.Lobby.LobbyState>();
        }
    }

    public void ByPass()
    {
        IsVerified = true;
        _netManager.ClientSendMessage(new MsgDiscordAuthByPass());
    }
}