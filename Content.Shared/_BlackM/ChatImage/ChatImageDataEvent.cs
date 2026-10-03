using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.ChatImage;

[Serializable, NetSerializable]
public sealed class ChatImageDataEvent : EntityEventArgs
{
    public int Id;
    public List<byte[]> Frames;
    public int[] DelaysMs;

    public ChatImageDataEvent(int id, List<byte[]> frames, int[] delaysMs)
    {
        Id = id;
        Frames = frames;
        DelaysMs = delaysMs;
    }
}
