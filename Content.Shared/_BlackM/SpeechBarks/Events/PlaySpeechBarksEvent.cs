using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.SpeechBarks;

[Serializable, NetSerializable]
public sealed class PlaySpeechBarksEvent : EntityEventArgs
{
    public NetEntity Source;
    public int MessageId;
    public SoundSpecifier Sound;
    public float Pitch;
    public float Variation;
    public float Speed;
    public BarkMode Mode;
    public bool IsWhisper;

    public PlaySpeechBarksEvent(
        NetEntity source,
        int messageId,
        SoundSpecifier sound,
        float pitch,
        float variation,
        float speed,
        BarkMode mode,
        bool isWhisper)
    {
        Source = source;
        MessageId = messageId;
        Sound = sound;
        Pitch = pitch;
        Variation = variation;
        Speed = speed;
        Mode = mode;
        IsWhisper = isWhisper;
    }
}
