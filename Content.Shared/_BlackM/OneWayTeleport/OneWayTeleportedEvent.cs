using Robust.Shared.GameObjects;

namespace Content.Shared._BlackM.OneWayTeleport;

public sealed class OneWayTeleportedEvent : EntityEventArgs
{
    public EntityUid Entity { get; }
    public string DestinationId { get; }

    public OneWayTeleportedEvent(EntityUid entity, string destinationId)
    {
        Entity = entity;
        DestinationId = destinationId;
    }
}
