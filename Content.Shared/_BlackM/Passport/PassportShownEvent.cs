using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Passport;

[Serializable, NetSerializable]
public sealed class PassportShownEvent : EntityEventArgs
{
    public PassportBoundUserInterfaceState State;

    public PassportShownEvent(PassportBoundUserInterfaceState state)
    {
        State = state;
    }
}
