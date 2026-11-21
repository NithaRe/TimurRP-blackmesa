using Content.Shared._BlackM.EngineeringBreakdown;
using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._BlackM.EngineeringBreakdown;

[RegisterComponent, Access(typeof(EngineeringBreakdownRule))]
public sealed partial class EngineeringBreakdownRuleComponent : Component
{
    [DataField(required: true)]
    public Dictionary<ProtoId<BreakdownPrototype>, float> Breakdowns = new();

    [DataField]
    public int MinTargets = 2;

    [DataField]
    public int MaxTargets = 4;

    [DataField]
    public TimeSpan Deadline = TimeSpan.FromMinutes(10);

    [DataField]
    public TimeSpan ReminderInterval = TimeSpan.FromMinutes(3);

    [DataField]
    public bool Escalate = true;

    [DataField]
    public int Reward = 15000;

    [DataField]
    public ProtoId<CargoAccountPrototype> RewardAccount = "Cargo";

    [DataField]
    public bool PayPartialOnFailure = true;

    [DataField]
    public LocId Sender = "engineering-breakdown-sender";

    [DataField]
    public LocId TargetsAnnouncement = "engineering-breakdown-announcement-targets";

    [DataField]
    public LocId ReminderAnnouncement = "engineering-breakdown-announcement-reminder";

    [DataField]
    public LocId ProgressAnnouncement = "engineering-breakdown-announcement-progress";

    [DataField]
    public LocId SuccessAnnouncement = "engineering-breakdown-announcement-success";

    [DataField]
    public LocId FailureAnnouncement = "engineering-breakdown-announcement-failure";

    [DataField]
    public SoundSpecifier? TargetsSound = new SoundPathSpecifier("/Audio/Announcements/attention.ogg");

    [DataField]
    public SoundSpecifier? ReminderSound = new SoundPathSpecifier("/Audio/_BlackM/Announcements/Announcers/Blackmesa/warning.ogg");

    [DataField]
    public SoundSpecifier? ProgressSound;

    [DataField]
    public SoundSpecifier? SuccessSound = new SoundPathSpecifier("/Audio/Misc/notice2.ogg");

    [DataField]
    public SoundSpecifier? FailureSound = new SoundPathSpecifier("/Audio/Announcements/power_off.ogg");

    [DataField]
    public Color AnnouncementColor = Color.FromHex("#EFB341");

    [DataField]
    public Color FailureColor = Color.FromHex("#E5463B");

    [DataField]
    public Color SuccessColor = Color.FromHex("#5CC86B");

    [ViewVariables]
    public EntityUid? Station;

    [ViewVariables]
    public HashSet<EntityUid> Targets = new();

    [ViewVariables]
    public int Total;

    [ViewVariables]
    public int Repaired;

    [ViewVariables]
    public int EarnedReward;

    [ViewVariables]
    public TimeSpan DeadlineAt;

    [ViewVariables]
    public TimeSpan NextReminder;

    [ViewVariables]
    public bool Finished;
}
