using Robust.Shared.Audio;

namespace Content.Server._BlackM.DeathSound;

[RegisterComponent, Access(typeof(DeathSoundSystem))]
public sealed partial class DeathSoundComponent : Component
{
    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_BlackM/Death/death.ogg",
        AudioParams.Default.WithVolume(-2f).WithMaxDistance(12f));
}
