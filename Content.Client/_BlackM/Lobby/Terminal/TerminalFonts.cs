using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Utility;

namespace Content.Client._BlackM.Lobby.Terminal;

public static class TerminalFonts
{
    public const string PrimaryBold = "/Fonts/_BlackM/Tektur-Regular.ttf";

    private static readonly ResPath[] FontPaths =
    [
        new(PrimaryBold),
        new("/Fonts/NotoSans/NotoSansSymbols-Bold.ttf"),
        new("/Fonts/NotoSans/NotoSansSymbols2-Regular.ttf"),
        new("/Fonts/NotoSans/NotoSansSC-Regular.ttf"),
    ];

    public static Font Bold(IResourceCache cache, int size)
    {
        return cache.GetFont(FontPaths, size);
    }
}
