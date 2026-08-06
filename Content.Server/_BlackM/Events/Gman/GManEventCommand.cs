using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;

namespace Content.Server._BlackM.Events.Gman;

[AdminCommand(AdminFlags.Fun)]
public sealed class GManEventCommand : LocalizedCommands
{
    [Dependency] private readonly IEntitySystemManager _sysMan = default!;
    [Dependency] private readonly IPlayerManager _playerMan = default!;

    public override string Command => "bm_gman";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1)
        {
            shell.WriteError("Использование: bm_gman <ник игрока>");
            return;
        }

        if (!_playerMan.TryGetSessionByUsername(args[0], out var session))
        {
            shell.WriteError($"Игрок с ником '{args[0]}' не найден.");
            return;
        }

        if (!_sysMan.GetEntitySystem<GManEventSystem>().StartEvent(session))
            shell.WriteError("Не удалось запустить ивент для этого игрока (не найден живой персонаж?).");
    }
}
