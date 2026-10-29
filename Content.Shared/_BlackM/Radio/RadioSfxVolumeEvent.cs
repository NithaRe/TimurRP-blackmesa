using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Radio;

[Serializable, NetSerializable]
public sealed class RadioSfxVolumeEvent(float gain) : EntityEventArgs
{
    public readonly float Gain = gain;
}
