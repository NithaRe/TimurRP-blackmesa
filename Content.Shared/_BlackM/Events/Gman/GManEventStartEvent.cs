using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Events.Gman;

[Serializable, NetSerializable]
public sealed class GManEventStartEvent : EntityEventArgs
{
    public float Duration;
}
