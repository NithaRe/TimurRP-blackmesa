using Robust.Shared.Prototypes;

namespace Content.Shared._BlackM.Achievements;

public enum AchievementKind : byte
{
    Playtime,
    Stat,
    Date,
    Manual,
}
[Prototype("blackmAchievement")]
public sealed class AchievementPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;
    [DataField(required: true)]
    public string Name { get; private set; } = string.Empty;
    [DataField(required: true)]
    public string Description { get; private set; } = string.Empty;
    [DataField(required: true)]
    public string Icon { get; private set; } = string.Empty;

    [DataField]
    public AchievementKind Kind { get; private set; } = AchievementKind.Playtime;
    [DataField]
    public string Tracker { get; private set; } = "Overall";
    [DataField]
    public float Hours { get; private set; }
    [DataField]
    public string Stat { get; private set; } = string.Empty;
    [DataField]
    public int Count { get; private set; }
    [DataField]
    public string DateFrom { get; private set; } = string.Empty;
    [DataField]
    public string DateTo { get; private set; } = string.Empty;
    [DataField]
    public int Order { get; private set; }
    public float Target => Kind switch
    {
        AchievementKind.Playtime => Hours,
        AchievementKind.Stat => Count,
        _ => 0f,
    };
}
