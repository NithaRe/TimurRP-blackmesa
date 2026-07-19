using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._BlackM.Megaphone;

[RegisterComponent, NetworkedComponent]
public sealed partial class MegaphoneComponent : Component
{
    [DataField]
    public string PhraseLocKey = "megaphone-phrase-next";

    [DataField]
    public string SpeakerNameLocKey = "megaphone-speaker-name";

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_BlackM/Megaphone/next.ogg");
}
