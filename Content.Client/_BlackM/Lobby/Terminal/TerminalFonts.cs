using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Log;

namespace Content.Client._BlackM.Lobby.Terminal;

public static class TerminalFonts
{
    public const string PrimaryBold = "/Fonts/_BlackM/JetBrainsMono/JetBrainsMono-Bold.ttf";
    private const string FallbackBold = "/Fonts/RobotoMono/RobotoMono-Bold.ttf";

    private static readonly Dictionary<int, Font> Cache = new();
    private static bool _logged;

    public static Font Bold(IResourceCache cache, int size)
    {
        if (Cache.TryGetValue(size, out var cached))
            return cached;

        var primaryExists = cache.ContentFileExists(PrimaryBold);
        if (!_logged)
        {
            _logged = true;
            var sawmill = IoCManager.Resolve<ILogManager>().GetSawmill("blackm.fonts");
            if (primaryExists)
                sawmill.Info($"Terminal font: {PrimaryBold}");
            else
                sawmill.Warning($"Terminal font NOT FOUND at {PrimaryBold}, falling back to RobotoMono");
        }

        var font = cache.GetFont(new[]
        {
            primaryExists ? PrimaryBold : FallbackBold,
            "/Fonts/NotoSans/NotoSansSymbols-Bold.ttf",
            "/Fonts/NotoSans/NotoSansSymbols2-Regular.ttf",
            "/Fonts/NotoSans/NotoSansSC-Regular.ttf",
        }, size);

        Cache[size] = font;
        return font;
    }
}
