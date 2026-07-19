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

[Serializable, NetSerializable]
public sealed class BattleMusicAnnounceMessage : EntityEventArgs
{
    public string Attacker;
    public string Defender;

    public BattleMusicAnnounceMessage(string attacker, string defender)
    {
        Attacker = attacker;
        Defender = defender;
    }
}
