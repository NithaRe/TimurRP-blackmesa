using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Underground;

[Serializable, NetSerializable]
public enum UndergroundMission : byte
{
    HeadcrabHunt,
    HoundeyeHunt,
    HeadcrabSample,
    HoundeyeSample,
    FastHeadcrabHunt,
    FastHeadcrabSample,
    LostResearchDisk,
}

public static class UndergroundMissions
{
    public static bool IsValid(UndergroundMission mission) => mission is UndergroundMission.HeadcrabHunt or UndergroundMission.HoundeyeHunt or UndergroundMission.HeadcrabSample or UndergroundMission.HoundeyeSample or UndergroundMission.FastHeadcrabHunt or UndergroundMission.FastHeadcrabSample or UndergroundMission.LostResearchDisk;
    public static bool IsSample(UndergroundMission mission) => mission is UndergroundMission.HeadcrabSample or UndergroundMission.HoundeyeSample or UndergroundMission.FastHeadcrabSample;
    public static bool IsItem(UndergroundMission mission) => mission == UndergroundMission.LostResearchDisk;
    public static bool IsDelivery(UndergroundMission mission) => IsSample(mission) || IsItem(mission);
    public static bool IsHoundeye(UndergroundMission mission) => mission is UndergroundMission.HoundeyeHunt or UndergroundMission.HoundeyeSample;
    public static int Goal(UndergroundMission mission) => IsDelivery(mission) ? 1 : mission == UndergroundMission.FastHeadcrabHunt || IsHoundeye(mission) ? 3 : 4;
    public static int DurationSeconds(UndergroundMission mission) => mission is UndergroundMission.FastHeadcrabHunt or UndergroundMission.FastHeadcrabSample ? 300 : 600;
    public static string Target(UndergroundMission mission) => IsHoundeye(mission) ? "MobHoundeyeBlackM" : "MobHeadcrabBlackM";
    public static string Sample(UndergroundMission mission) => IsHoundeye(mission) ? "BlackMUndergroundHoundeyeSample" : "BlackMUndergroundHeadcrabSample";
    public static string Reward(UndergroundMission mission) => RewardAmount(mission) == 2500 ? "SpaceCash2500" : "SpaceCash1000";
    public static int RewardAmount(UndergroundMission mission) => IsHoundeye(mission) || DurationSeconds(mission) == 300 || IsItem(mission) ? 2500 : 1000;
}

[Serializable, NetSerializable]
public sealed class UndergroundMissionSelectMessage(UndergroundMission mission) : BoundUserInterfaceMessage
{
    public readonly UndergroundMission Mission = mission;
}

[Serializable, NetSerializable]
public sealed class UndergroundMissionClaimMessage : BoundUserInterfaceMessage;
