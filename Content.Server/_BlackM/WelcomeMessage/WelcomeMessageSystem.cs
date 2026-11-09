using Content.Server.GameTicking;
using Content.Shared._BlackM.WelcomeMessage;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.WelcomeMessage;

public sealed class WelcomeMessageSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(2.5);

    private readonly List<WelcomeMessageSetPrototype>[] _tiers =
    {
        new(), new(), new(), new(),
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
    }

    private void OnSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId == null || !_proto.TryIndex<JobPrototype>(ev.JobId, out var job))
            return;

        if (PickSet(job.ID) is not { } set)
            return;

        var text = Loc.GetString(_random.Pick(set.Messages),
            ("name", ev.Profile.Name),
            ("job", job.LocalizedName));

        var msg = new WelcomeMessageEvent(
            Loc.GetString(set.Sender),
            Loc.GetString(set.Title),
            text,
            set.Portrait,
            set.PortraitCropPos,
            set.PortraitCropSize,
            set.Accent,
            set.Duration);

        var session = ev.Player;
        Timer.Spawn(Delay, () =>
        {
            if (session.Status == SessionStatus.InGame)
                RaiseNetworkEvent(msg, session);
        });
    }

    private WelcomeMessageSetPrototype? PickSet(string jobId)
    {
        foreach (var tier in _tiers)
        {
            tier.Clear();
        }

        foreach (var set in _proto.EnumeratePrototypes<WelcomeMessageSetPrototype>())
        {
            if (set.Messages.Count == 0)
                continue;

            if (set.Jobs.Contains(jobId))
            {
                _tiers[0].Add(set);
                continue;
            }

            if (set.Jobs.Count == 0 && set.Departments.Count == 0)
            {
                _tiers[3].Add(set);
                continue;
            }

            var tier = -1;
            foreach (var departmentId in set.Departments)
            {
                if (!_proto.TryIndex(departmentId, out var department) || !department.Roles.Contains(jobId))
                    continue;

                tier = department.Primary ? 1 : 2;
                if (tier == 1)
                    break;
            }

            if (tier > 0)
                _tiers[tier].Add(set);
        }

        foreach (var tier in _tiers)
        {
            if (tier.Count > 0)
                return _random.Pick(tier);
        }

        return null;
    }
}
