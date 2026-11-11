using Content.Shared.Administration;
using JetBrains.Annotations;
using Robust.Shared.Console;

namespace Content.Client._BlackM.Monorail;

public static class MonorailPlacementSettings
{
    public static float Step = 1f;

    public static bool Magnet = true;

    public static string StepName => Step switch
    {
        >= 0.99f => "tile",
        >= 0.49f => "half",
        > 0f => "quarter",
        _ => "free",
    };
}

[UsedImplicitly, AnyCommand]
public sealed class MonorailSnapCommand : LocalizedCommands
{
    private static readonly string[] Options = { "tile", "half", "quarter", "free", "magnet", "nomagnet" };

    public override string Command => "monorailsnap";

    public override string Help => LocalizationManager.GetString($"cmd-{Command}-help", ("command", Command));

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        foreach (var arg in args)
        {
            switch (arg.ToLowerInvariant())
            {
                case "tile":
                    MonorailPlacementSettings.Step = 1f;
                    break;
                case "half":
                    MonorailPlacementSettings.Step = 0.5f;
                    break;
                case "quarter":
                    MonorailPlacementSettings.Step = 0.25f;
                    break;
                case "free":
                    MonorailPlacementSettings.Step = 0f;
                    break;
                case "magnet":
                    MonorailPlacementSettings.Magnet = true;
                    break;
                case "nomagnet":
                    MonorailPlacementSettings.Magnet = false;
                    break;
                default:
                    shell.WriteError(LocalizationManager.GetString($"cmd-{Command}-error", ("arg", arg)));
                    return;
            }
        }

        shell.WriteLine(LocalizationManager.GetString($"cmd-{Command}-status",
            ("step", MonorailPlacementSettings.StepName),
            ("magnet", MonorailPlacementSettings.Magnet ? "on" : "off")));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return CompletionResult.FromHintOptions(Options, LocalizationManager.GetString($"cmd-{Command}-hint"));
    }
}
