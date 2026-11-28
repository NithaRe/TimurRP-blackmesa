using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.ZenRift;

[Serializable, NetSerializable]
public enum ZenRiftPhase : byte
{
    Opening,
    Active,
    Closing,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ZenRiftVisualsComponent : Component
{
    [DataField, AutoNetworkedField]
    public ZenRiftPhase Phase = ZenRiftPhase.Opening;

    [DataField, AutoNetworkedField]
    public TimeSpan PhaseStart;

    [DataField, AutoNetworkedField]
    public TimeSpan PhaseDuration;

    [DataField]
    public float SpinSpeed = 25f;

    [DataField]
    public float MaxScale = 1.25f;
}

[Serializable, NetSerializable]
public record struct ZenRiftMarker(NetEntity Target, Vector2 Position, MapId Map, Color Color);

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ZenRiftNavigatorComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<ZenRiftMarker> Markers = new();

    public override bool SendOnlyToOwner => true;
}
