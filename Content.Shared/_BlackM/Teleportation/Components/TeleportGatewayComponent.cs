using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Teleportation.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TeleportGatewayComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? LinkedPortal;

    [DataField]
    public string PairKey = "default";
}

[Serializable, NetSerializable]
public enum TeleportGatewayVisuals : byte
{
    Linked
}

public enum TeleportGatewayVisualLayers : byte
{
    On
}