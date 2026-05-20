using Content.Server._BlackM.Passport;
using Content.Server.Administration;
using Content.Shared._BlackM.Passport;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.IoC;

namespace Content.Server._BlackM.Passport.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class PassportErrorCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entMan = default!;

    public string Command     => "passport_error";
    public string Description => "passport-error-command-description";
    public string Help        => "passport-error-command-help";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("passport-error-command-help"));
            return;
        }

        if (!EntityUid.TryParse(args[0], out var uid))
        {
            shell.WriteError(Loc.GetString("passport-error-command-invalid-uid", ("uid", args[0])));
            return;
        }

        if (!_entMan.TryGetComponent<PassportComponent>(uid, out var comp))
        {
            shell.WriteError(Loc.GetString("passport-error-command-not-passport", ("uid", args[0])));
            return;
        }

        var system = _entMan.EntitySysManager.GetEntitySystem<PassportSystem>();
        system.ForceApplyBureaucraticError(uid, comp);

        shell.WriteLine(Loc.GetString("passport-error-command-success",
            ("uid", uid),
            ("field", comp.ErrorField),
            ("value", comp.ErrorValue)));
    }
}
