using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.BattleMusic;

[Serializable, NetSerializable]
public sealed class BattleMusicStartMessage : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class BattleMusicStopMessage : EntityEventArgs
{
}
