using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;

namespace Content.Client._BlackM.Lobby.Terminal;

public static class TerminalFonts
{
    public const string PrimaryBold = "/Fonts/_BlackM/Tektur-Regular.ttf";

    private static readonly Dictionary<int, Font> Cache = new();

    public static Font Bold(IResourceCache cache, int size)
    {
        if (Cache.TryGetValue(size, out var cached))
            return cached;

        var font = cache.GetFont(new[]
        {
            PrimaryBold,
            "/Fonts/NotoSans/NotoSansSymbols-Bold.ttf",
            "/Fonts/NotoSans/NotoSansSymbols2-Regular.ttf",
            "/Fonts/NotoSans/NotoSansSC-Regular.ttf",
        }, size);

        Cache[size] = font;
        return font;
    }
}
