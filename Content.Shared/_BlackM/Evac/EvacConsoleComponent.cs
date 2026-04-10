using Robust.Shared.Prototypes;

namespace Content.Shared._BlackM.Evac;

public enum EvacConsoleState : byte
{
    Idle,
    Countdown,
    Open,
}

[RegisterComponent]
public sealed partial class EvacConsoleComponent : Component
{
    [DataField]
    public float OpenDelay = 300f;

    [DataField]
    public float CloseDelay = 300f;

    [DataField]
    public TimeSpan? TargetTime;

    [DataField]
    public EvacConsoleState State = EvacConsoleState.Idle;

    /// <summary>Прототип портала выхода на карте эвакуации.</summary>
    [DataField]
    public EntProtoId PortalDestPrototype = "EvacPortalDestination";

    /// <summary>Заспавненный портал на карте эвакуации.</summary>
    [DataField]
    public EntityUid? SpawnedPortalDest;
}