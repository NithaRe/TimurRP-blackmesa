using Robust.Shared.Audio;

namespace Content.Shared._BlackM.Access;

[RegisterComponent]
public sealed partial class AccessButtonComponent : Component
{
    [DataField]
    public string Port = "Pressed";

    [DataField]
    public SoundSpecifier? SoundAllow = new SoundPathSpecifier("/Audio/_BlackM/Access/allowbeep.ogg");

    [DataField]
    public SoundSpecifier? SoundDeny = new SoundPathSpecifier("/Audio/_BlackM/Access/denybeep.ogg");

    [DataField]
    public float PressCooldown = 0.5f;

    [DataField]
    public TimeSpan NextPressAllowed = TimeSpan.Zero;
}
