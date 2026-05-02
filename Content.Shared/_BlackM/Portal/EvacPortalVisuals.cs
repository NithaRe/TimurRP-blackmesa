using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Portal;

[Serializable, NetSerializable]
public enum EvacPortalVisualLayers : byte
{
    Active,
    Portal,
}

[Serializable, NetSerializable]
public enum EvacPortalVisuals : byte
{
    Active,
}