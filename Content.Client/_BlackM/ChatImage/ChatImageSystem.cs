using System.IO;
using Content.Shared._BlackM.ChatImage;
using Robust.Client.Graphics;

namespace Content.Client._BlackM.ChatImage;

public sealed class ChatImageSystem : EntitySystem
{
    [Dependency] private readonly IClyde _clyde = default!;

    private const int MaxFrames = 64; // защита на стороне клиента

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ChatImageDataEvent>(OnData);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        ChatImageStore.Clear();
    }

    private void OnData(ChatImageDataEvent ev)
    {
        var count = Math.Min(ev.Frames.Count, MaxFrames);
        if (count == 0 || ev.DelaysMs.Length < count)
            return;

        var frames = new Texture[count];
        for (var i = 0; i < count; i++)
        {
            using var stream = new MemoryStream(ev.Frames[i]);
            frames[i] = _clyde.LoadTextureFromPNGStream(stream, $"chatimage-{ev.Id}-{i}");
        }

        ChatImageStore.Set(ev.Id, new ChatImageData(frames, ev.DelaysMs));
    }
}
