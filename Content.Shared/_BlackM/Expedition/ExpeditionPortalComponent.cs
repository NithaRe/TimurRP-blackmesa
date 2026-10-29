using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Expedition;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ExpeditionPortalComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Linked;

    [DataField]
    public bool IsExit;

    [DataField]
    public EntityUid? Map;

    [DataField, AutoNetworkedField]
    public bool Active;

    [DataField, AutoNetworkedField]
    public TimeSpan? OpenedAt;

    [DataField, AutoNetworkedField]
    public TimeSpan? ClosesAt;

    [DataField]
    public SoundSpecifier? EnterSound;

    [DataField]
    public SoundSpecifier? ExitSound;
}

[Serializable, NetSerializable]
public enum ExpeditionPortalVisuals : byte
{
    State
}

[Serializable, NetSerializable]
public enum ExpeditionPortalState : byte
{
    Off,
    On
}

[Serializable, NetSerializable]
public enum ExpeditionPortalLayers : byte
{
    Vortex,
    Lights
}
