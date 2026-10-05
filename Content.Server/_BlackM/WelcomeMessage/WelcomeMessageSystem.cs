using System.Linq;
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

    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(2);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
    }

    private void OnSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId == null || !_proto.TryIndex<JobPrototype>(ev.JobId, out var job))
            return;

        var all = _proto.EnumeratePrototypes<WelcomeMessageSetPrototype>().ToList();

        var specific = all.Where(s => s.Jobs.Contains(job.ID)).ToList();
        var pool = specific.Count > 0 ? specific : all.Where(s => s.Jobs.Count == 0).ToList();
        if (pool.Count == 0)
            return;

        var set = _random.Pick(pool);
        if (set.Messages.Count == 0)
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
            set.Duration);

        var session = ev.Player;
        Timer.Spawn(Delay, () =>
        {
            if (session.Status == SessionStatus.InGame)
                RaiseNetworkEvent(msg, session);
        });
    }
}