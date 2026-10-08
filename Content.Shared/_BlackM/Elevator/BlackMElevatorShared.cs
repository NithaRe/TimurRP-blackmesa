using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Elevator;

[Serializable, NetSerializable]
public enum BlackMElevatorState : byte
{
    Idle,
    Boarding,
    Descending,
    Cooldown,
}

public enum BlackMElevatorLightMode : byte
{
    Normal,
    Dim,
    Emergency,
}

[Serializable, NetSerializable]
public sealed class BlackMElevatorFadeEvent : EntityEventArgs
{
    public readonly bool ToBlack;
    public readonly float Duration;
    public readonly string? Text;

    public BlackMElevatorFadeEvent(bool toBlack, float duration, string? text = null)
    {
        ToBlack = toBlack;
        Duration = duration;
        Text = text;
    }
}