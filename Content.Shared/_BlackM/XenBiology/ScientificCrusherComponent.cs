using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._BlackM.XenBiology;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ScientificCrusherComponent : Component
{
    /// <summary>
    /// Whether the crusher is currently processing its contents.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Crushing;

    /// <summary>
    /// Time when the current processing cycle ends.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan CrushEndTime;

    /// <summary>
    /// Duration of one processing cycle.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan CrushDuration = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Prototype spawned when a minor incident occurs.
    /// </summary>
    [DataField]
    public EntProtoId IncidentEffect = "EffectSparks";

    /// <summary>
    /// Prototype spawned to create a lingering radiation hazard.
    /// </summary>
    [DataField]
    public EntProtoId IncidentRadiationEffect = "XenExperimentRadiationPulse";

    /// <summary>
    /// Radius of the electrical discharge around the crusher.
    /// </summary>
    [DataField]
    public float IncidentShockRadius = 3f;

    /// <summary>
    /// Electrical damage dealt to nearby living entities.
    /// </summary>
    [DataField]
    public int IncidentShockDamage = 25;

    /// <summary>
    /// Duration of the stun caused by the electrical discharge.
    /// </summary>
    [DataField]
    public TimeSpan IncidentShockTime = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Damage applied to the crusher when a minor incident occurs.
    /// </summary>
    [DataField]
    public DamageSpecifier IncidentDamage = new();
}

[Serializable, NetSerializable]
public enum ScientificCrusherVisuals : byte
{
    Crushing
}
