using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.EngineeringBreakdown;

[Serializable, NetSerializable]
public sealed partial class BreakdownRepairDoAfterEvent : SimpleDoAfterEvent;

[ByRefEvent]
public readonly record struct BreakdownResolvedEvent(EntityUid Device, EntityUid? User, bool Repaired, int Reward);
