using Robust.Shared.Configuration;

namespace Content.Shared._BlackM.CCVar;

[CVarDefs]
public sealed class BlackMCVars
{
    public static readonly CVarDef<bool> HardcodeZoomEnabled =
        CVarDef.Create("hardcode.zoom_enabled", false, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> HardcodeZoomLevel =
        CVarDef.Create("hardcode.zoom_level", 0.5f, CVar.SERVER | CVar.REPLICATED);
}