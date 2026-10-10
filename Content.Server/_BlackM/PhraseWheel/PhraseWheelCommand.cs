using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.Administration;
using Content.Shared._BlackM.PhraseWheel;
using Content.Shared.Administration;
using Content.Shared.Ghost;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._BlackM.PhraseWheel;

[AdminCommand(AdminFlags.Admin)]
public sealed class PhraseWheelCommand : LocalizedCommands
{
    private const string AllArg = "all";
    private const string RevokeAllArg = "revokeall";

    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPrototypeManager _protos = default!;

    public override string Command => "phrasewheel";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1)
        {
            shell.WriteError(Help);
            return;
        }

        var system = _entities.System<PhraseWheelSystem>();
        var target = args[0];

        if (target.Equals(RevokeAllArg, StringComparison.OrdinalIgnoreCase))
        {
            var count = 0;
            foreach (var uid in GetNonGhostEntities())
            {
                if (system.RevokeAccess(uid))
                    count++;
            }

            shell.WriteLine(Loc.GetString("cmd-phrasewheel-revokeall-done", ("count", count)));
            return;
        }

        if (!TryParseCategories(shell, args, out var categories))
            return;

        var list = string.Join(", ", categories.Select(c => c.Id));

        if (target.Equals(AllArg, StringComparison.OrdinalIgnoreCase))
        {
            var count = 0;
            foreach (var uid in GetNonGhostEntities())
            {
                system.GrantAccess(uid, new HashSet<ProtoId<PhraseWheelCategoryPrototype>>(categories));
                count++;
            }

            shell.WriteLine(categories.Count == 0
                ? Loc.GetString("cmd-phrasewheel-all-done-full", ("count", count))
                : Loc.GetString("cmd-phrasewheel-all-done-categories", ("count", count), ("categories", list)));
            return;
        }

        var session = _players.Sessions
            .FirstOrDefault(s => s.Name.Equals(target, StringComparison.OrdinalIgnoreCase));

        if (session?.AttachedEntity is not { } entity)
        {
            shell.WriteError(Loc.GetString("cmd-phrasewheel-error-player-not-found", ("name", target)));
            return;
        }

        switch (system.UpdateAccess(entity, categories))
        {
            case PhraseWheelAccessResult.Revoked:
                shell.WriteLine(Loc.GetString("cmd-phrasewheel-revoked", ("name", target)));
                break;
            case PhraseWheelAccessResult.Updated:
                shell.WriteLine(Loc.GetString("cmd-phrasewheel-updated",
                    ("name", target), ("categories", list)));
                break;
            default:
                shell.WriteLine(categories.Count == 0
                    ? Loc.GetString("cmd-phrasewheel-granted-full", ("name", target))
                    : Loc.GetString("cmd-phrasewheel-granted-categories",
                        ("name", target), ("categories", list)));
                break;
        }
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var options = _players.Sessions
                .Select(s => new CompletionOption(s.Name))
                .Append(new CompletionOption(AllArg, Loc.GetString("cmd-phrasewheel-completion-all")))
                .Append(new CompletionOption(RevokeAllArg, Loc.GetString("cmd-phrasewheel-completion-revokeall")));

            return CompletionResult.FromHintOptions(options, Loc.GetString("cmd-phrasewheel-hint-target"));
        }

        if (args[0].Equals(RevokeAllArg, StringComparison.OrdinalIgnoreCase))
            return CompletionResult.Empty;

        var categories = _protos.EnumeratePrototypes<PhraseWheelCategoryPrototype>()
            .Select(c => new CompletionOption(c.ID));

        return CompletionResult.FromHintOptions(categories, Loc.GetString("cmd-phrasewheel-hint-category"));
    }

    private IEnumerable<EntityUid> GetNonGhostEntities()
    {
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } uid || _entities.HasComponent<GhostComponent>(uid))
                continue;

            yield return uid;
        }
    }

    private bool TryParseCategories(IConsoleShell shell, string[] args,
        out HashSet<ProtoId<PhraseWheelCategoryPrototype>> categories)
    {
        categories = new HashSet<ProtoId<PhraseWheelCategoryPrototype>>();

        foreach (var raw in args.Skip(1))
        {
            if (!_protos.HasIndex<PhraseWheelCategoryPrototype>(raw))
            {
                shell.WriteError(Loc.GetString("cmd-phrasewheel-error-unknown-category", ("category", raw)));
                return false;
            }

            categories.Add(raw);
        }

        return true;
    }
}
