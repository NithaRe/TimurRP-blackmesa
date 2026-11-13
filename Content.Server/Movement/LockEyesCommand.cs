// SPDX-FileCopyrightText: 2022 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.Server.Movement;

[AdminCommand(AdminFlags.Fun)]
public sealed class LockEyesCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IConfigurationManager _cfg = default!; // BlackM

    public override string Command => $"lockeyes";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("shell-need-exactly-one-argument"));
            return;
        }

        if (!bool.TryParse(args[0], out var value))
        {
            shell.WriteError(Loc.GetString("parse-bool-fail", ("args", args[0])));
            return;
        }

        // BlackM
        _cfg.SetCVar(CCVars.CameraRotationLocked, value);
    }
}