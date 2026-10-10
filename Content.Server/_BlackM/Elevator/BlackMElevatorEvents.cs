using Robust.Shared.GameObjects;

namespace Content.Server._BlackM.Elevator;

public sealed class BlackMElevatorArrivedEvent : EntityEventArgs
{
    public readonly EntityUid Entity;
    public readonly string ElevatorId;

    public BlackMElevatorArrivedEvent(EntityUid entity, string elevatorId)
    {
        Entity = entity;
        ElevatorId = elevatorId;
    }
}
