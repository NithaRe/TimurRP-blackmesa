using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._BlackM.ForceField;

[RegisterComponent, NetworkedComponent]
public sealed partial class PassportForceFieldComponent : Component
{
    [DataField("passSound")]
    public SoundSpecifier PassSound = new SoundPathSpecifier("/Audio/_BlackM/ForceField/passport_field_pass.ogg");

    [DataField("denySound")]
    public SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/_BlackM/ForceField/passport_field_deny.ogg");

    [DataField("bumpCooldown")]
    public TimeSpan BumpCooldown = TimeSpan.FromSeconds(0.8);

    public readonly Dictionary<EntityUid, TimeSpan> NextInteractionAllowed = new();
}
