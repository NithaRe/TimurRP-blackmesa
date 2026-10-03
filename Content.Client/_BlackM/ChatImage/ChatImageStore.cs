using System.Diagnostics.CodeAnalysis;
using Robust.Client.Graphics;

namespace Content.Client._BlackM.ChatImage;

public sealed class ChatImageData
{
    public readonly Texture[] Frames;
    public readonly int[] DelaysMs;
    public readonly int TotalMs;

    public ChatImageData(Texture[] frames, int[] delaysMs)
    {
        Frames = frames;
        DelaysMs = delaysMs;

        var total = 0;
        foreach (var d in delaysMs)
            total += d;

        TotalMs = Math.Max(1, total);
    }
}

public static class ChatImageStore
{
    private static readonly Dictionary<int, ChatImageData> Images = new();

    public static void Set(int id, ChatImageData data) => Images[id] = data;

    public static bool TryGet(int id, [NotNullWhen(true)] out ChatImageData? data) => Images.TryGetValue(id, out data);

    public static void Clear() => Images.Clear();
}
