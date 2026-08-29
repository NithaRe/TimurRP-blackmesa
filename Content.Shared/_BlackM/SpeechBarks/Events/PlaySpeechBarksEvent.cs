using Robust.Shared.Serialization;
using Robust.Shared.Audio;

namespace Content.Shared._BlackM.SpeechBarks;

[Serializable, NetSerializable]
public sealed class PlaySpeechBarksEvent : EntityEventArgs
{
    public NetEntity? Source;
    public string? Message;
    public SoundSpecifier SoundSpecifier;
    public float Pitch;
    public float LowVariation;
    public float HighVariation;
    public bool IsWhisper;

    public PlaySpeechBarksEvent(
        NetEntity source,
        string? message,
        SoundSpecifier soundSpecifier,
        float pitch,
        float lowVariation,
        float highVariation,
        bool isWhisper)
    {
        Source = source;
        Message = message;
        SoundSpecifier = soundSpecifier;
        Pitch = pitch;
        LowVariation = lowVariation;
        HighVariation = highVariation;
        IsWhisper = isWhisper;
    }
}
