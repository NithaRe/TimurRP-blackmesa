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
    CVarDef.Create("blackm.lobby.animation",
        "/Textures/_BlackM/LobbyScreens/lobbyscreen.rsi,/Textures/_BlackM/LobbyScreens/lobbyscreen2.rsi", CVar.CLIENT | CVar.ARCHIVE);

    public static readonly CVarDef<string> LobbyArt =
        CVarDef.Create("blackm.lobby.art", "random", CVar.CLIENT | CVar.ARCHIVE);

    public static readonly CVarDef<string> LobbyBackgroundPreset =
        CVarDef.Create("blackm.lobby.background_preset", "default", CVar.CLIENT | CVar.ARCHIVE);

    public static readonly CVarDef<bool> EchoEnabled =
        CVarDef.Create("blackm.echo_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> EchoStrongPreset =
        CVarDef.Create("blackm.echo_strong_preset", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> DefaultAtmosphereShaderEnabled =
        CVarDef.Create("blackm.default_atmosphere_shader_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> BattleMusicEnabled =
        CVarDef.Create("audio.battle_music_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> BattleMusicVolume =
        CVarDef.Create("audio.battle_music_volume", 1.0f, CVar.CLIENTONLY | CVar.ARCHIVE);
    
    public static readonly CVarDef<bool> BarksEnabled =
        CVarDef.Create("blackm.barks_enabled", true, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<bool> ReplaceTTSWithBarks =
        CVarDef.Create("blackm.replace_tts_with_barks", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> BarksVolume =
        CVarDef.Create("blackm.barks_volume", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> BarksMinPitch =
        CVarDef.Create("blackm.barks_min_pitch", 0.5f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> BarksMaxPitch =
        CVarDef.Create("blackm.barks_max_pitch", 2f, CVar.SERVER | CVar.REPLICATED);
    
    public static readonly CVarDef<float> LightBloomStrength =
        CVarDef.Create("blackm.light_bloom_strength", 0.7f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> MusicRadioVolume =
        CVarDef.Create("blackm.music_radio_volume", 1.0f, CVar.CLIENTONLY | CVar.ARCHIVE);
}
