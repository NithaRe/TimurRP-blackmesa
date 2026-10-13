using System;
using System.IO;
using System.Threading;
using Content.Client.Gameplay;
using Content.Client.Lobby;
using Content.Shared._BlackM.CCVar;
using Content.Shared._BlackM.DiscordAuth;
using Robust.Client.Graphics;
using Robust.Client.State;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Client._BlackM.DiscordAuth;

public sealed partial class DiscordAuthManager : Content.Corvax.Interfaces.Client.IClientDiscordAuthManager
{
    [Dependency] private IClientNetManager _netManager = default!;
    [Dependency] private IStateManager _stateManager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    public string AuthUrl { get; private set; } = string.Empty;
    public Texture? Qrcode { get; private set; }
    public string LinkCode { get; private set; } = string.Empty;
    public bool IsVerified { get; private set; } = false;
    public bool IsOpt { get; private set; }
    public bool IsEnabled { get; private set; }

    public event Action? DataUpdated;

    private readonly CancellationTokenSource _watchdogCancel = new();

    private bool _authRequired;

    private bool _bypassed;

    private Type? _returnState;

    public void Initialize()
    {
        _netManager.RegisterNetMessage<MsgDiscordAuthCheck>();
        _netManager.RegisterNetMessage<MsgDiscordAuthByPass>();
        _netManager.RegisterNetMessage<MsgDiscordAuthRequired>(OnDiscordAuthRequired);
        _netManager.RegisterNetMessage<MsgDiscordAuthVerified>(OnDiscordAuthVerified);

        _cfg.OnValueChanged(BlackMCVars.DiscordAuthIsOptional, v => IsOpt = v, true);
        _cfg.OnValueChanged(BlackMCVars.DiscordAuthEnabled, v => IsEnabled = v, true);

        _netManager.Disconnect += OnDisconnected;

        Timer.SpawnRepeating(TimeSpan.FromMilliseconds(100), EnforceAuthState, _watchdogCancel.Token);
    }

    private void OnDisconnected(object? sender, NetDisconnectedArgs args)
    {
        _authRequired = false;
        _bypassed = false;
        _returnState = null;
        IsVerified = false;
        AuthUrl = string.Empty;
        LinkCode = string.Empty;
        Qrcode = null;
    }

    private void EnforceAuthState()
    {
        if (!_authRequired || IsVerified || IsOpt || !_netManager.IsConnected)
            return;

        EnterAuthState();
    }

    private void EnterAuthState()
    {
        var current = _stateManager.CurrentState;
        if (current is DiscordAuthState)
            return;

        _returnState = current.GetType();
        _stateManager.RequestStateChange<DiscordAuthState>();
    }

    private void OnDiscordAuthRequired(MsgDiscordAuthRequired message)
    {
        if (_bypassed)
            return;

        IsVerified = false;
        _authRequired = true;

        AuthUrl = message.AuthUrl;
        LinkCode = message.LinkCode;
        if (message.QrCode.Length > 0)
        {
            using var ms = new MemoryStream(message.QrCode);
            Qrcode = Texture.LoadFromPNGStream(ms);
        }

        EnterAuthState();

        DataUpdated?.Invoke();
    }

    private void OnDiscordAuthVerified(MsgDiscordAuthVerified message)
    {
        IsVerified = true;
        _authRequired = false;

        if (_stateManager.CurrentState is DiscordAuthState)
        {
            var target = _returnState == typeof(GameplayState)
                ? typeof(GameplayState)
                : typeof(LobbyState);

            _stateManager.RequestStateChange(target);
        }

        _returnState = null;
    }

    public void ByPass()
    {
        IsVerified = true;
        _bypassed = true;
        _authRequired = false;
        _netManager.ClientSendMessage(new MsgDiscordAuthByPass());
    }
}
