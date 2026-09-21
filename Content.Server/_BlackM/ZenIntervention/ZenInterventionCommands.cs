using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server._BlackM.ZenIntervention;

[AdminCommand(AdminFlags.Debug)]
public sealed class ZenStatusCommand : IConsoleCommand
{
    public string Command => "zen_status";
    public string Description => "see status.";
    public string Help => "zen_status";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var sys = IoCManager.Resolve<IEntityManager>().System<Content.Server._BlackM.ZenIntervention.ZenInterventionSystem>();

        if (!sys.TryGetComponent(out var comp))
        {
            shell.WriteLine("fail get status.");
            return;
        }

        shell.WriteLine($"level: {comp.Level:0.##} / {comp.MaxLevel}");
        shell.WriteLine($"explosion: {comp.BreachThreshold} (done: {comp.BreachTriggered})");
        shell.WriteLine($"active {comp.WaveActive}, interval {comp.WaveInterval}");
    }
}

[AdminCommand(AdminFlags.Debug)]
public sealed class ZenSetLevelCommand : IConsoleCommand
{
    public string Command => "zen_setlevel";
    public string Description => "(0..100).";
    public string Help => "zen_setlevel";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1 || !float.TryParse(args[0], out var value))
        {
            shell.WriteError("use: zen_setlevel <0-100>");
            return;
        }

        var sys = IoCManager.Resolve<IEntityManager>().System<Content.Server._BlackM.ZenIntervention.ZenInterventionSystem>();
        sys.SetLevel(value);

        shell.WriteLine($"set level: {value}");
    }
}

[AdminCommand(AdminFlags.Debug)]
public sealed class ZenAddLevelCommand : IConsoleCommand
{
    public string Command => "zen_addlevel";
    public string Description => "xz.";
    public string Help => "zen_addlevel xz";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1 || !float.TryParse(args[0], out var delta))
        {
            shell.WriteError("xz");
            return;
        }

        var sys = IoCManager.Resolve<IEntityManager>().System<Content.Server._BlackM.ZenIntervention.ZenInterventionSystem>();
        sys.AdjustLevel(delta);

        shell.WriteLine($"set: {delta}");
    }
}

[AdminCommand(AdminFlags.Debug)]
public sealed class ZenTriggerBreachCommand : IConsoleCommand
{
    public string Command => "zen_triggerbreach";
    public string Description => "force.";
    public string Help => "zen_triggerbreach";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var sys = IoCManager.Resolve<IEntityManager>().System<Content.Server._BlackM.ZenIntervention.ZenInterventionSystem>();
        sys.ForceTriggerBreach();

        shell.WriteLine("make expl.");
    }
}

[AdminCommand(AdminFlags.Debug)]
public sealed class ZenTriggerWaveCommand : IConsoleCommand
{
    public string Command => "zen_triggerwave";
    public string Description => "one wave.";
    public string Help => "zen_triggerwave";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var sys = IoCManager.Resolve<IEntityManager>().System<Content.Server._BlackM.ZenIntervention.ZenInterventionSystem>();
        sys.ForceSpawnWave();

        shell.WriteLine("wave summon.");
    }
}

[AdminCommand(AdminFlags.Debug)]
public sealed class ZenResetCommand : IConsoleCommand
{
    public string Command => "zen_reset";
    public string Description => "reset %.";
    public string Help => "zen_reset";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var sys = IoCManager.Resolve<IEntityManager>().System<Content.Server._BlackM.ZenIntervention.ZenInterventionSystem>();
        sys.ResetState();

        shell.WriteLine("progress reset +.");
    }
}
