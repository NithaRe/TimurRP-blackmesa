using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.AltActionEffect;

public sealed class AltInteractedEvent : EntityEventArgs
{
    public readonly EntityUid User;
    public readonly EntityUid Target;

    public AltInteractedEvent(EntityUid user, EntityUid target)
    {
        User = user;
        Target = target;
    }
}

[Serializable, NetSerializable]
public sealed class AltActionEffectEvent : EntityEventArgs
{
    public readonly NetEntity User;
    public readonly NetEntity Target;

    public AltActionEffectEvent(NetEntity user, NetEntity target)
    {
        User = user;
        Target = target;
    }
}
