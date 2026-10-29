using Robust.Shared.Audio;

namespace Content.Server._BlackM.Radio;

[RegisterComponent]
public sealed partial class RadioSfxComponent : Component
{
    [DataField]
    public SoundSpecifier? SendStartSound = new SoundCollectionSpecifier("BlackMRadioSendStart");

    [DataField]
    public SoundSpecifier? SendEndSound = new SoundCollectionSpecifier("BlackMRadioSendEnd");

    [DataField]
    public SoundSpecifier? ReceiveSound = new SoundCollectionSpecifier("BlackMRadioReceive");

    [DataField]
    public SoundSpecifier? StaticSound = new SoundCollectionSpecifier("BlackMRadioStatic");

    [DataField]
    public SoundSpecifier? AmbientSound = new SoundCollectionSpecifier("BlackMRadioAmbient");

    [DataField]
    public AudioParams PrivateParams = AudioParams.Default.WithVolume(-3f).WithVariation(0.04f);

    [DataField]
    public AudioParams StaticParams = AudioParams.Default.WithVolume(-9f).WithVariation(0.06f);

    [DataField]
    public AudioParams AmbientParams = AudioParams.Default.WithVolume(-12f).WithMaxDistance(3f).WithVariation(0.08f);

    [DataField]
    public TimeSpan SendCooldown = TimeSpan.FromSeconds(0.6);

    [DataField]
    public TimeSpan ReceiveCooldown = TimeSpan.FromSeconds(1.5);

    [DataField]
    public TimeSpan AmbientCooldown = TimeSpan.FromSeconds(3);

    [DataField]
    public float EndDelayBase = 0.3f;

    [DataField]
    public float EndDelayPerChar = 0.035f;

    [ViewVariables] public TimeSpan NextSendSound;
    [ViewVariables] public TimeSpan NextReceiveSound;
    [ViewVariables] public TimeSpan NextAmbientSound;
}