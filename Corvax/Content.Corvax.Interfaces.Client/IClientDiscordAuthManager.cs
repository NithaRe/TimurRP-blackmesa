using System;
using Content.Corvax.Interfaces.Shared;

namespace Content.Corvax.Interfaces.Client;

public interface IClientDiscordAuthManager : ISharedDiscordAuthManager
{
    string LinkCode { get; }

    bool IsVerified { get; }

    bool IsOpt { get; }

    bool IsEnabled { get; }

    event Action? DataUpdated;

    void ByPass();
}