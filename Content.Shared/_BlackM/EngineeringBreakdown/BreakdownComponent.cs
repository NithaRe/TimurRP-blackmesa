using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._BlackM.EngineeringBreakdown;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BreakdownComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<BreakdownPrototype> Breakdown;

    [DataField, AutoNetworkedField]
    public int Step;

    [ViewVariables]
    public EntityUid? Rule;

    [ViewVariables]
    public EntityUid? AttachedEffect;

    [ViewVariables]
    public TimeSpan NextBurst;

    [ViewVariables]
    public EntityUid? RepairUser;

    [ViewVariables]
    public int RepairStep = -1;

    [ViewVariables]
    public TimeSpan RepairLockUntil;

    [ViewVariables] public bool RestoreBreaker;
    [ViewVariables] public bool RestorePowerDisabled;
    [ViewVariables] public bool RestoreBolts;
    [ViewVariables] public bool RestoreBattery;
    [ViewVariables] public bool RestoreBlink;
}
