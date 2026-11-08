using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Vehicles;

[Serializable, NetSerializable]
public enum BMCarVisualLayers : byte
{
    WheelFrontLeft,
    WheelFrontRight,
    WheelRearLeft,
    WheelRearRight,
    Headlights,
    Taillights,
    Brakelights,
    Lightbar,
}
