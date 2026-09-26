using System.Linq;
using Content.Server._BlackM.Ams;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Shared._BlackM.OneWayTeleport;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Server.Player;
using Robust.Shared.Enums;

namespace Content.Server._BlackM.RoundEnd;

public sealed class AmsRoundEndSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private const string EvacuationDestinationId = "ams_evacuation";

    private bool _tracking;
    private int _initialPoolSize;
    private int _voluntaryExcludedCount;

    private readonly HashSet<EntityUid> _pending = new();

    private readonly HashSet<EntityUid> _finalDead = new();

    private readonly HashSet<EntityUid> _evacuated = new();

    private readonly Dictionary<EntityUid, EntityUid> _lastKnownBody = new();

    private readonly Dictionary<EntityUid, bool> _lastKnownWasDeadOrCrit = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AmsLaunchedEvent>(OnAmsLaunched);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);
        SubscribeLocalEvent<OneWayTeleportedEvent>(OnTeleported);
    }

    private void OnAmsLaunched(AmsLaunchedEvent ev)
    {
        StartTracking();
    }

    private void StartTracking()
    {
        if (_tracking)
            return;

        _tracking = true;
        _pending.Clear();
        _finalDead.Clear();
        _evacuated.Clear();
        _lastKnownBody.Clear();
        _lastKnownWasDeadOrCrit.Clear();
        _voluntaryExcludedCount = 0;

        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status != SessionStatus.InGame)
                continue;

            if (!_mind.TryGetMind(session, out var mindId, out var mind))
                continue;

            _pending.Add(mindId);

            if (mind.OwnedEntity is { } body)
            {
                _lastKnownBody[mindId] = body;
                _lastKnownWasDeadOrCrit[mindId] = IsBodyDeadOrCrit(body);
            }
        }

        _initialPoolSize = _pending.Count;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_tracking || _pending.Count == 0)
            return;

        foreach (var mindId in _pending.ToArray())
        {
            if (!TryComp<MindComponent>(mindId, out var mind))
                continue;

            var currentBody = mind.OwnedEntity;

            if (currentBody is { } body && Exists(body))
            {
                var hadLastBody = _lastKnownBody.TryGetValue(mindId, out var lastBody);
                var isDeadOrCrit = IsBodyDeadOrCrit(body);

                if (hadLastBody && lastBody != body)
                {
                    var oldWasDeadOrCrit = _lastKnownWasDeadOrCrit.TryGetValue(mindId, out var wasDeadOrCrit) && wasDeadOrCrit;

                    if (!oldWasDeadOrCrit)
                    {
                        RemoveVoluntary(mindId);
                        continue;
                    }
                }

                _lastKnownBody[mindId] = body;
                _lastKnownWasDeadOrCrit[mindId] = isDeadOrCrit;
                continue;
            }

            var wasAliveBeforeGone = _lastKnownWasDeadOrCrit.TryGetValue(mindId, out var wasDeadOrCritFlag) && !wasDeadOrCritFlag;

            if (wasAliveBeforeGone)
            {
                RemoveVoluntary(mindId);
            }
            else
            {
                _pending.Remove(mindId);
                _lastKnownBody.Remove(mindId);
                _lastKnownWasDeadOrCrit.Remove(mindId);
                _finalDead.Add(mindId);
            }
        }
    }

    private void RemoveVoluntary(EntityUid mindId)
    {
        _pending.Remove(mindId);
        _lastKnownBody.Remove(mindId);
        _lastKnownWasDeadOrCrit.Remove(mindId);
        _voluntaryExcludedCount++;
    }

    private bool IsBodyDeadOrCrit(EntityUid body)
    {
        return TryComp<MobStateComponent>(body, out var state) && IsCountedAsDead(state.CurrentState);
    }

    private static bool IsCountedAsDead(MobState state)
    {
        return state is MobState.Critical or MobState.Dead;
    }

    private void OnTeleported(OneWayTeleportedEvent ev)
    {
        if (!_tracking || ev.DestinationId != EvacuationDestinationId)
            return;

        if (!_mind.TryGetMind(ev.Entity, out var mindId, out _))
            return;

        if (!_pending.Contains(mindId))
            return; 

        if (IsBodyDeadOrCrit(ev.Entity))
            return;

        _pending.Remove(mindId);
        _lastKnownBody.Remove(mindId);
        _lastKnownWasDeadOrCrit.Remove(mindId);
        _evacuated.Add(mindId);
    }

    private void OnRoundEndText(RoundEndTextAppendEvent ev)
    {
        if (!_tracking || _initialPoolSize == 0)
            return;

        var deadNow = _finalDead.Count;
        var aliveOnStation = 0;

        foreach (var mindId in _pending)
        {
            if (!TryComp<MindComponent>(mindId, out var mind) || mind.OwnedEntity is not { } body || !Exists(body))
            {
                deadNow++;
                continue;
            }

            if (IsBodyDeadOrCrit(body))
                deadNow++;
            else
                aliveOnStation++;
        }

        var totalPool = _initialPoolSize - _voluntaryExcludedCount;
        if (totalPool <= 0)
            return;

        var percent = (int)Math.Round(_evacuated.Count * 100f / totalPool);

        var resultLoc = percent switch
        {
            0 => "ams-roundend-total-defeat",
            <= 20 => "ams-roundend-major-antag-victory",
            <= 50 => "ams-roundend-minor-antag-victory",
            <= 75 => "ams-roundend-minor-crew-victory",
            <= 99 => "ams-roundend-major-crew-victory",
            _ => "ams-roundend-perfect-crew-victory",
        };

        var evacuatedLine = Loc.GetString("ams-roundend-stat-evacuated", ("count", _evacuated.Count));
        var deadLine = Loc.GetString("ams-roundend-stat-dead", ("count", deadNow));
        var remainingLine = Loc.GetString("ams-roundend-stat-remaining", ("count", aliveOnStation));
        var totalLine = Loc.GetString("ams-roundend-stat-total", ("total", totalPool), ("percent", percent));
        var resultLine = Loc.GetString(resultLoc);

        ev.AddLine(string.Empty);
        ev.AddLine($"[color=#e63946]{evacuatedLine}[/color]");
        ev.AddLine($"[color=#e63946]{deadLine}[/color]");
        ev.AddLine($"[color=#e63946]{remainingLine}[/color]");
        ev.AddLine($"[color=#e63946]{totalLine}[/color]");
        ev.AddLine(string.Empty);
        ev.AddLine($"[color=#e63946]{resultLine}[/color]");
    }
}
