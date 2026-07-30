using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Ams;

[NetSerializable, Serializable]
public enum AmsStatus : byte
{
    Inactive,
    Synchronizing,
    Ready,
    Active,
    Used,
}
