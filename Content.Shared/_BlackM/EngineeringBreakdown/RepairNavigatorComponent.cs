using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.EngineeringBreakdown;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RepairNavigatorComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public List<RepairMarker> Markers = new();

    public override bool SendOnlyToOwner => true;
}

[Serializable, NetSerializable]
public record struct RepairMarker(NetEntity Target, Vector2 Position, MapId Map, Color Color, int Severity);
