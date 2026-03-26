using Content.Shared._BlackM.CCVar;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.Server._BlackM.Hardcode;

[AdminCommand(AdminFlags.Admin)]
public sealed class HardcodeDamageCommand : IConsoleCommand
{
    public string Command => "hardcode_damage";
    public string Description => "Управление множителем урона от оружия (пули, лазеры).";
    public string Help => "hardcode_damage 3 — включить x3\nhardcode_damage 1 — вернуть дефолт\nhardcode_damage — показать статус";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var cfg = IoCManager.Resolve<IConfigurationManager>();

        if (args.Length == 0)
        {
            var proj = cfg.GetCVar(CCVars.PlaytestProjectileDamageModifier);
            var hitscan = cfg.GetCVar(CCVars.PlaytestHitscanDamageModifier);
            shell.WriteLine($"Множитель пуль: x{proj}, лазеров: x{hitscan}");
            return;
        }

        if (!float.TryParse(args[0], out var multiplier) || multiplier <= 0)
        {
            shell.WriteError("Укажи число больше 0. Например: hardcode_damage 3");
            return;
        }

        cfg.SetCVar(CCVars.PlaytestProjectileDamageModifier, multiplier);
        cfg.SetCVar(CCVars.PlaytestHitscanDamageModifier, multiplier);
        shell.WriteLine($"Множитель урона от оружия установлен: x{multiplier}");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class HardcodeZoomCommand : IConsoleCommand
{
    public string Command => "hardcode_zoom";
    public string Description => "Управление зумом камеры для всех игроков.";
    public string Help => "hardcode_zoom true 0.5 — включить зум\nhardcode_zoom false — выключить\nhardcode_zoom — показать статус";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var cfg = IoCManager.Resolve<IConfigurationManager>();

        if (args.Length == 0)
        {
            var on = cfg.GetCVar(BlackMCVars.HardcodeZoomEnabled);
            var zoom = cfg.GetCVar(BlackMCVars.HardcodeZoomLevel);
            shell.WriteLine($"Hardcode zoom: {(on ? "✅ включён" : "❌ выключен")}, уровень: {zoom} (x{1f / zoom:F1})");
            return;
        }

        if (!bool.TryParse(args[0], out var enabled))
        {
            shell.WriteError("Первый аргумент должен быть true или false");
            return;
        }

        if (args.Length >= 2)
        {
            if (float.TryParse(args[1], out var zoom) && zoom > 0 && zoom <= 2f)
                cfg.SetCVar(BlackMCVars.HardcodeZoomLevel, zoom);
            else
                shell.WriteError("Неверный зум. Допустимые значения: 0.1 — 2.0");
        }

        cfg.SetCVar(BlackMCVars.HardcodeZoomEnabled, enabled);
        var lvl = cfg.GetCVar(BlackMCVars.HardcodeZoomLevel);
        shell.WriteLine($"Hardcode zoom {(enabled ? "✅ включён" : "❌ выключен")}, уровень: {lvl} (x{1f / lvl:F1})");
    }
}