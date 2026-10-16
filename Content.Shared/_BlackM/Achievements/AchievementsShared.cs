using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Achievements;

public static class AchievementsConst
{
    public const int MaxBadges = 8;
}
public static class AchievementStats
{
    public const string RoundsPlayed = "rounds_played";
    public const string AmsParts = "ams_parts";
}
[Serializable, NetSerializable]
public sealed class AchievementsRequestEvent : EntityEventArgs
{
}
[Serializable, NetSerializable]
public sealed class AchievementSelectionEvent : EntityEventArgs
{
    public List<string> Selected;

    public AchievementSelectionEvent(List<string> selected)
    {
        Selected = selected;
    }
}
[Serializable, NetSerializable]
public sealed class AchievementsStateEvent : EntityEventArgs
{
    public Dictionary<string, float> Progress;
    public List<string> Unlocked;
    public List<string> Selected;

    public AchievementsStateEvent(Dictionary<string, float> progress, List<string> unlocked, List<string> selected)
    {
        Progress = progress;
        Unlocked = unlocked;
        Selected = selected;
    }
}
