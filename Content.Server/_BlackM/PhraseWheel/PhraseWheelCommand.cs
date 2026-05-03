using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;
using System.Linq;
using System;

namespace Content.Server._BlackM.PhraseWheel;

[AdminCommand(AdminFlags.Admin)]
public sealed class PhraseWheelCommand : IConsoleCommand
{
    public string Command => "phrasewheel";
    public string Description => "Выдать/забрать доступ к меню фраз";
    public string Help => "Использование: phrasewheel <ник> [категория...]\nБез категорий — выдаёт/забирает доступ ко всем фразам.";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1)
        {
            shell.WriteError(Help);
            return;
        }

        var name = args[0];
        var playerManager = IoCManager.Resolve<IPlayerManager>();
        var entityManager = IoCManager.Resolve<IEntityManager>();

        var targetSession = playerManager.Sessions
            .FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (targetSession?.AttachedEntity == null)
        {
            shell.WriteError($"player '{name}' no found in game.");
            return;
        }

        var uid = targetSession.AttachedEntity.Value;
        var categories = args.Skip(1).ToList();

        var sys = entityManager.System<PhraseWheelSystem>();
        sys.UpdateAccess(uid, categories, name, shell);
    }
}