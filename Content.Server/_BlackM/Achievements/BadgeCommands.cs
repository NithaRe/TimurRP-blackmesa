using Content.Server._BlackM.Passport;
using Content.Server.Administration;
using Content.Shared._BlackM.Achievements;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.IoC;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._BlackM.Achievements.Commands;

internal static class BadgeCommandHelper
{
    public static bool TryGetTarget(IConsoleShell shell, IPlayerManager players, string[] args, int index,
        out ICommonSession session)
    {
        if (args.Length > index)
        {
            if (players.TryGetSessionByUsername(args[index], out var found))
            {
                session = found;
                return true;
            }

            shell.WriteError(Loc.GetString("blackm-badge-cmd-no-player", ("name", args[index])));
            session = default!;
            return false;
        }

        if (shell.Player is { } self)
        {
            session = self;
            return true;
        }

        shell.WriteError(Loc.GetString("blackm-badge-cmd-need-player"));
        session = default!;
        return false;
    }
    public static void RefreshPassport(IEntityManager entMan, ICommonSession session)
    {
        if (session.AttachedEntity is { } mob)
            entMan.System<PassportSystem>().RefreshBadges(mob);
    }

    public static CompletionResult Ids(IPrototypeManager proto, bool withAll)
    {
        var list = new List<string>();
        if (withAll)
            list.Add("all");

        foreach (var p in proto.EnumeratePrototypes<AchievementPrototype>())
            list.Add(p.ID);

        return CompletionResult.FromHintOptions(list, Loc.GetString("blackm-badge-cmd-hint-id"));
    }
}
[AdminCommand(AdminFlags.Admin)]
public sealed class GiveBadgeCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IPlayerManager _players = default!;

    public string Command     => "givebadge";
    public string Description => "blackm-badge-give-description";
    public string Help        => "blackm-badge-give-help";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1 || args.Length > 2)
        {
            shell.WriteError(Loc.GetString("blackm-badge-give-help"));
            return;
        }

        if (!BadgeCommandHelper.TryGetTarget(shell, _players, args, 1, out var session))
            return;

        var system = _entMan.System<AchievementsSystem>();

        if (args[0] == "all")
        {
            var count = 0;
            foreach (var p in _proto.EnumeratePrototypes<AchievementPrototype>())
            {
                if (system.Grant(session, p.ID, select: true))
                    count++;
            }

            BadgeCommandHelper.RefreshPassport(_entMan, session);
            shell.WriteLine(Loc.GetString("blackm-badge-give-all", ("count", count), ("player", session.Name)));
            return;
        }

        if (!system.Grant(session, args[0], select: true))
        {
            shell.WriteError(Loc.GetString("blackm-badge-cmd-unknown-id", ("id", args[0])));
            return;
        }

        BadgeCommandHelper.RefreshPassport(_entMan, session);
        shell.WriteLine(Loc.GetString("blackm-badge-give-ok", ("id", args[0]), ("player", session.Name)));
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1 ? BadgeCommandHelper.Ids(_proto, true) : CompletionResult.Empty;
    }
}
[AdminCommand(AdminFlags.Admin)]
public sealed class TakeBadgeCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IPlayerManager _players = default!;

    public string Command     => "takebadge";
    public string Description => "blackm-badge-take-description";
    public string Help        => "blackm-badge-take-help";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1 || args.Length > 2)
        {
            shell.WriteError(Loc.GetString("blackm-badge-take-help"));
            return;
        }

        if (!BadgeCommandHelper.TryGetTarget(shell, _players, args, 1, out var session))
            return;

        var system = _entMan.System<AchievementsSystem>();
        var count = 0;

        if (args[0] == "all")
        {
            foreach (var p in _proto.EnumeratePrototypes<AchievementPrototype>())
            {
                if (system.Revoke(session, p.ID))
                    count++;
            }
        }
        else if (system.Revoke(session, args[0]))
        {
            count = 1;
        }

        BadgeCommandHelper.RefreshPassport(_entMan, session);
        shell.WriteLine(Loc.GetString("blackm-badge-take-ok", ("count", count), ("player", session.Name)));
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1 ? BadgeCommandHelper.Ids(_proto, true) : CompletionResult.Empty;
    }
}
[AdminCommand(AdminFlags.Admin)]
public sealed class BadgesCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IPlayerManager _players = default!;

    public string Command     => "badges";
    public string Description => "blackm-badge-list-description";
    public string Help        => "blackm-badge-list-help";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!BadgeCommandHelper.TryGetTarget(shell, _players, args, 0, out var session))
            return;

        var system = _entMan.System<AchievementsSystem>();
        shell.WriteLine(Loc.GetString("blackm-badge-list-title", ("player", session.Name)));

        var protos = new List<AchievementPrototype>(_proto.EnumeratePrototypes<AchievementPrototype>());
        protos.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.ID, b.ID));

        foreach (var p in protos)
        {
            var mark = system.IsUnlocked(session, p.ID) ? "[x]" : "[ ]";
            var progress = p.Target > 0
                ? $" {system.GetProgress(session, p):0.#}/{p.Target:0.#}"
                : string.Empty;

            shell.WriteLine($"{mark} {p.ID} ({p.Kind}){progress}");
        }
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return CompletionResult.Empty;
    }
}
[AdminCommand(AdminFlags.Admin)]
public sealed class BadgeStatCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _players = default!;

    public string Command     => "badgestat";
    public string Description => "blackm-badge-stat-description";
    public string Help        => "blackm-badge-stat-help";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2 || args.Length > 3 || !long.TryParse(args[1], out var amount))
        {
            shell.WriteError(Loc.GetString("blackm-badge-stat-help"));
            return;
        }

        if (!BadgeCommandHelper.TryGetTarget(shell, _players, args, 2, out var session))
            return;

        var system = _entMan.System<AchievementsSystem>();
        system.AddStat(session, args[0], amount);
        BadgeCommandHelper.RefreshPassport(_entMan, session);

        shell.WriteLine(Loc.GetString("blackm-badge-stat-ok",
            ("stat", args[0]), ("value", system.GetStat(session, args[0])), ("player", session.Name)));
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length != 1)
            return CompletionResult.Empty;

        return CompletionResult.FromHintOptions(
            new[] { AchievementStats.RoundsPlayed, AchievementStats.AmsParts },
            Loc.GetString("blackm-badge-cmd-hint-stat"));
    }
}
