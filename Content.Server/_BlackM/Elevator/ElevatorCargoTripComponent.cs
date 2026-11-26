using System.Numerics;
using Robust.Shared.GameObjects;

namespace Content.Server._BlackM.Elevator;

[RegisterComponent]
public sealed partial class BlackMElevatorCargoTripComponent : Component
{
    public EntityUid Target;
    public readonly Dictionary<EntityUid, Vector2> Offsets = new();
}

[ByRefEvent]
public record struct BlackMElevatorCargoTripFinishedEvent(bool Succeeded);
