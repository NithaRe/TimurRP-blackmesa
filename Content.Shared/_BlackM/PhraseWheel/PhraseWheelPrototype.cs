using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._BlackM.PhraseWheel;

[Prototype("phraseWheelEntry")]
public sealed partial class PhraseWheelEntryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Text { get; private set; } = string.Empty;

    [DataField]
    public PhraseWheelChatType ChatType { get; private set; } = PhraseWheelChatType.Speak;

    [DataField]
    public SoundSpecifier? Sound { get; private set; }

    [DataField]
    public SpriteSpecifier Icon { get; private set; } =
        new SpriteSpecifier.Texture(new("/Textures/Interface/phrasewheel.png"));

    [DataField]
    public Color Color { get; private set; } = Color.MediumPurple;

    [DataField]
    public string? TextColor { get; private set; }

    [DataField]
    public string Label { get; private set; } = string.Empty;

    /// <summary>razdel menu</summary>
    [DataField]
    public string Category { get; private set; } = "global";

    [DataField]
    public int Order { get; private set; } = 0;

    /// <summary>player color text.</summary>
    [DataField]
    public bool AllowCustomColor { get; private set; } = true;
}

public enum PhraseWheelChatType : byte
{
    Speak,
    Whisper,
    Emote,
    Shout,
}
