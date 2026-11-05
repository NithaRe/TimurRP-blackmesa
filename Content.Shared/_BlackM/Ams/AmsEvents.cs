using System;
using System.Numerics;
using Content.Shared.DoAfter;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Ams;

[Serializable, NetSerializable]
public sealed partial class AmsReinforceDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public sealed class AmsBlastEvent(int map, Vector2 epicenter, float speed, float maxRadius, TimeSpan start)
    : EntityEventArgs
{
    public readonly int Map = map;
    public readonly Vector2 Epicenter = epicenter;
    public readonly float Speed = speed;
    public readonly float MaxRadius = maxRadius;
    public readonly TimeSpan Start = start;
}
