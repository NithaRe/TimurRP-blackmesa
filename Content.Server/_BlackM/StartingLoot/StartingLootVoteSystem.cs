using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Voting;
using Content.Server.Voting.Managers;
using Content.Shared._BlackM.CCVar;
using Content.Shared._BlackM.StartingLoot;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.StartingLoot;

public sealed class StartingLootVoteSystem : EntitySystem
{
    [Dependency] private readonly IVoteManager _votes = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IChatManager _chat = default!;

    public bool Enabled { get; private set; }

    private bool _voteStarted;
    private IVoteHandle? _activeVote;

    public override void Initialize()
    {
        base.Initialize();
        Enabled = !_cfg.GetCVar(BlackMCVars.StartingLootVoteRequired);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
    }

    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        _voteStarted = false;
        Enabled = !_cfg.GetCVar(BlackMCVars.StartingLootVoteRequired);
        _activeVote?.Cancel();
        _activeVote = null;
    }

    public override void Update(float frameTime)
    {
        if (_ticker.RunLevel != GameRunLevel.PreRoundLobby)
        {
            _activeVote?.Cancel();
            _activeVote = null;
            return;
        }

        var required = _cfg.GetCVar(BlackMCVars.StartingLootVoteRequired);
        if (!required)
        {
            Enabled = true;
            return;
        }

        if (_voteStarted || _ticker.Paused || _ticker.RoundStartTime == TimeSpan.Zero)
            return;

        var timeLeft = _ticker.RoundStartTime - _timing.CurTime;
        var startAt = TimeSpan.FromSeconds(_cfg.GetCVar(BlackMCVars.StartingLootVoteStartSeconds));
        if (timeLeft <= TimeSpan.Zero || timeLeft > startAt)
            return;

        StartVote(timeLeft);
    }

    private void StartVote(TimeSpan timeLeft)
    {
        if (_voteStarted)
            return;

        _voteStarted = true;

        var maxDuration = TimeSpan.FromSeconds(_cfg.GetCVar(BlackMCVars.StartingLootVoteDurationSeconds));
        var options = new VoteOptions
        {
            Title = Loc.GetString("blackm-startloot-vote-title"),
            InitiatorText = Loc.GetString("blackm-startloot-vote-initiator"),
            Duration = TimeSpan.FromSeconds(Math.Min(maxDuration.TotalSeconds, Math.Max(5, timeLeft.TotalSeconds - 2)))
        };
        options.Options.Add((Loc.GetString("blackm-startloot-vote-yes"), true));
        options.Options.Add((Loc.GetString("blackm-startloot-vote-no"), false));

        _activeVote = _votes.CreateVote(options);
        _chat.DispatchServerAnnouncement(Loc.GetString("blackm-startloot-vote-title"));
        _activeVote.OnFinished += (_, args) =>
        {
            Enabled = args.Winner is true;
            var key = Enabled ? "blackm-startloot-vote-result-yes" : "blackm-startloot-vote-result-no";
            _chat.DispatchServerAnnouncement(Loc.GetString(key));
            _activeVote = null;
        };
        _activeVote.OnCancelled += _ =>
        {
            _activeVote = null;
        };
    }
}