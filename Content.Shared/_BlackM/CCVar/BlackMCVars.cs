using Robust.Shared.Configuration;

namespace Content.Shared._BlackM.CCVar;

[CVarDefs]
public sealed class BlackMCVars
{
    public static readonly CVarDef<bool> HardcodeZoomEnabled =
        CVarDef.Create("hardcode.zoom_enabled", false, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> HardcodeZoomLevel =
        CVarDef.Create("hardcode.zoom_level", 0.5f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<string> LobbyBackgroundType =
        CVarDef.Create("blackm.lobby.background_type", "animation", CVar.CLIENT | CVar.ARCHIVE);

    public static readonly CVarDef<string> LobbyAnimation =
        CVarDef.Create("blackm.lobby.animation", "/Textures/_BlackM/LobbyScreens/lobbyscreen.rsi", CVar.CLIENT | CVar.ARCHIVE);

    public static readonly CVarDef<string> LobbyArt =
        CVarDef.Create("blackm.lobby.art", "random", CVar.CLIENT | CVar.ARCHIVE);

    public static readonly CVarDef<string> LobbyBackgroundPreset =
        CVarDef.Create("blackm.lobby.background_preset", "default", CVar.CLIENT | CVar.ARCHIVE);
}