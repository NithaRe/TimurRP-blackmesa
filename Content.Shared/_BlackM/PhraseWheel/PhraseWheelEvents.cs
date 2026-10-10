using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.PhraseWheel;

[Serializable, NetSerializable]
public sealed class PlayPhraseWheelMessage : EntityEventArgs
{
    public ProtoId<PhraseWheelEntryPrototype> Phrase { get; init; }
    public Color? CustomColor { get; init; }
}

[Serializable, NetSerializable]
public sealed class PhraseWheelIconEvent : EntityEventArgs
{
    public NetEntity Source { get; init; }
    public ProtoId<PhraseWheelEntryPrototype> Phrase { get; init; }
}
