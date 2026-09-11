using System;
using Content.Corvax.Interfaces.Shared;
using Robust.Client.Graphics;

namespace Content.Corvax.Interfaces.Client;

public interface IClientDiscordAuthManager : ISharedDiscordAuthManager
{
    string AuthUrl { get; }

    Texture? Qrcode { get; }

    bool IsVerified { get; }

    bool IsOpt { get; }

    bool IsEnabled { get; }

    event Action? DataUpdated;

    void ByPass();
}