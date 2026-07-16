using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.PhraseWheel;

[Serializable, NetSerializable]
public sealed class PlayPhraseWheelMessage : EntityEventArgs
{
    public string PhraseId { get; init; } = string.Empty;
    public string? CustomColor { get; init; }
}

[Serializable, NetSerializable]
public sealed class PhraseWheelIconEvent : EntityEventArgs
{
    public NetEntity Source { get; init; }
    public string IconPath { get; init; } = string.Empty;
}
