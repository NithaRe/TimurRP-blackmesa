using Content.Server.Administration;
using Content.Shared._BlackM.NameTag;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Localization;

namespace Content.Server._BlackM.NameTag;

[AdminCommand(AdminFlags.Admin)]
public sealed class SetNameTagCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _players = default!;

    public string Command => "setnametag";
    public string Description => Loc.GetString("cmd-setnametag-desc");
    public string Help => Loc.GetString("cmd-setnametag-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1)
        {
            shell.WriteError(Loc.GetString("cmd-setnametag-error-usage", ("help", Help)));
            return;
        }

        if (!_players.TryGetSessionByUsername(args[0], out var session))
        {
            shell.WriteError(Loc.GetString("cmd-setnametag-error-player-not-found", ("name", args[0])));
            return;
        }

        if (session.AttachedEntity is not { } uid)
        {
            shell.WriteError(Loc.GetString("cmd-setnametag-error-no-entity"));
            return;
        }

        var sys = _entMan.System<NameTagSystem>();

        if (args.Length == 1)
        {
            sys.RemoveTag(uid);
            shell.WriteLine(Loc.GetString("cmd-setnametag-removed", ("name", args[0])));
            return;
        }

        var text = args[1];
        var color = Color.White;
        var rainbow = true;
        var style = NameTagAuraStyle.None;

        for (var i = 2; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg.StartsWith('#'))
            {
                if (Color.TryFromHex(arg) is not { } parsed)
                {
                    shell.WriteError(Loc.GetString("cmd-setnametag-error-bad-color", ("color", arg)));
                    return;
                }

                color = parsed;
            }
            else if (arg.Equals("norainbow", StringComparison.OrdinalIgnoreCase))
            {
                rainbow = false;
            }
            else if (arg.Equals("aura", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("rune", StringComparison.OrdinalIgnoreCase))
            {
                style = NameTagAuraStyle.Rune;
            }
            else if (arg.Equals("flame", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("fire", StringComparison.OrdinalIgnoreCase))
            {
                style = NameTagAuraStyle.Flame;
            }
            else if (arg.Equals("orbit", StringComparison.OrdinalIgnoreCase))
            {
                style = NameTagAuraStyle.Orbit;
            }
            else
            {
                shell.WriteError(Loc.GetString("cmd-setnametag-error-unknown-arg", ("arg", arg)));
                return;
            }
        }

        sys.SetTag(uid, text, color, rainbow, style);
        shell.WriteLine(Loc.GetString("cmd-setnametag-set", ("name", args[0])));
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(
                CompletionHelper.SessionNames(), Loc.GetString("cmd-setnametag-hint-ckey")),
            2 => CompletionResult.FromHint(Loc.GetString("cmd-setnametag-hint-text")),
            _ => CompletionResult.FromHint(Loc.GetString("cmd-setnametag-hint-options"))
        };
    }
}