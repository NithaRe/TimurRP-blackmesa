using System.Linq;
using System.Text;
using Content.Server.Cargo.Systems;
using Content.Server.StationEvents.Events;
using Content.Shared._BlackM.EngineeringBreakdown;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Chat;
using Content.Shared.Random.Helpers;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._BlackM.EngineeringBreakdown;

public sealed partial class EngineeringBreakdownRule : StationEventSystem<EngineeringBreakdownRuleComponent>
{
    [Dependency] private BreakdownSystem _breakdown = default!;
    [Dependency] private CargoSystem _cargo = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EngineeringBreakdownRuleComponent, BreakdownResolvedEvent>(OnResolved);
    }

    protected override void Started(EntityUid uid, EngineeringBreakdownRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        if (!TryGetRandomStation(out var station) || !TryComp<StationDataComponent>(station, out var data))
        {
            ForceEndSelf(uid, gameRule);
            return;
        }

        comp.Station = station;
        var grids = data.Grids;

        var candidates = new Dictionary<ProtoId<BreakdownPrototype>, List<EntityUid>>();
        foreach (var protoId in comp.Breakdowns.Keys)
        {
            if (!PrototypeManager.TryIndex(protoId, out var proto))
                continue;

            var list = new List<EntityUid>();
            var query = AllEntityQuery<TransformComponent, MetaDataComponent>();
            while (query.MoveNext(out var ent, out var xform, out _))
            {
                if (!xform.Anchored || xform.GridUid is not { } grid || !grids.Contains(grid))
                    continue;

                if (!_breakdown.CanBreak(ent, proto))
                    continue;

                list.Add(ent);
            }

            if (list.Count > 0)
                candidates[protoId] = list;
        }

        var count = RobustRandom.Next(comp.MinTargets, comp.MaxTargets + 1);
        for (var i = 0; i < count && candidates.Count > 0; i++)
        {
            var weights = comp.Breakdowns
                .Where(kv => candidates.ContainsKey(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);
            if (weights.Count == 0)
                break;

            var protoId = RobustRandom.Pick(weights);
            var list = candidates[protoId];
            var target = RobustRandom.PickAndTake(list);
            if (list.Count == 0)
                candidates.Remove(protoId);

            if (_breakdown.TryBreak(target, protoId, uid))
                comp.Targets.Add(target);
        }

        if (comp.Targets.Count == 0)
        {
            ForceEndSelf(uid, gameRule);
            return;
        }

        comp.Total = comp.Targets.Count;
        comp.Repaired = 0;
        comp.EarnedReward = 0;
        comp.Finished = false;
        comp.DeadlineAt = Timing.CurTime + comp.Deadline;
        comp.NextReminder = Timing.CurTime + comp.ReminderInterval;

        Announce(comp, Loc.GetString(comp.TargetsAnnouncement, ("list", BuildList(comp.Targets))),
            comp.TargetsSound, comp.AnnouncementColor);
    }

    protected override void ActiveTick(EntityUid uid, EngineeringBreakdownRuleComponent comp, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, comp, gameRule, frameTime);

        if (comp.Finished || comp.Total == 0)
            return;

        comp.Targets.RemoveWhere(t => TerminatingOrDeleted(t) || !HasComp<BreakdownComponent>(t));

        if (comp.Targets.Count == 0)
        {
            Finish(uid, comp, true);
            return;
        }

        var now = Timing.CurTime;
        if (now >= comp.DeadlineAt)
        {
            Finish(uid, comp, false);
            return;
        }

        if (now >= comp.NextReminder)
        {
            comp.NextReminder = now + comp.ReminderInterval;
            Announce(comp, Loc.GetString(comp.ReminderAnnouncement, ("list", BuildList(comp.Targets))),
                comp.ReminderSound, comp.AnnouncementColor);
        }
    }

    protected override void Ended(EntityUid uid, EngineeringBreakdownRuleComponent comp, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, comp, gameRule, args);

        comp.Finished = true;
        foreach (var target in comp.Targets.ToArray())
        {
            if (!TerminatingOrDeleted(target))
                _breakdown.ResolveBreakdown(target, repaired: false);
        }
        comp.Targets.Clear();
    }

    private void OnResolved(Entity<EngineeringBreakdownRuleComponent> ent, ref BreakdownResolvedEvent args)
    {
        var comp = ent.Comp;
        if (!comp.Targets.Remove(args.Device) || comp.Finished || !args.Repaired)
            return;

        comp.Repaired++;
        comp.EarnedReward += Math.Max(0, args.Reward);

        if (comp.Targets.Count == 0)
        {
            Finish(ent, comp, true);
            return;
        }

        Announce(comp, Loc.GetString(comp.ProgressAnnouncement,
                ("location", FormattedMessage.EscapeText(_breakdown.GetLocation(args.Device)))),
            comp.ProgressSound, comp.SuccessColor, onlyEngineers: true);
    }

    private void Finish(EntityUid uid, EngineeringBreakdownRuleComponent comp, bool success)
    {
        if (comp.Finished)
            return;

        comp.Finished = true;

        if (success)
        {
            if (comp.Repaired > 0)
            {
                var paid = PayReward(comp, comp.EarnedReward + comp.Reward);
                var message = new StringBuilder(Loc.GetString(comp.SuccessAnnouncement));
                if (paid > 0)
                    message.Append('\n').Append(Loc.GetString("engineering-breakdown-announcement-reward", ("amount", paid)));

                Announce(comp, message.ToString(), comp.SuccessSound, comp.SuccessColor);
            }
        }
        else
        {
            var list = BuildList(comp.Targets);
            foreach (var target in comp.Targets.ToArray())
            {
                if (TerminatingOrDeleted(target))
                    continue;

                if (comp.Escalate)
                    _breakdown.Escalate(target);
                else
                    _breakdown.ResolveBreakdown(target, repaired: false);
            }
            comp.Targets.Clear();

            var message = new StringBuilder(Loc.GetString(comp.FailureAnnouncement,
                ("list", list),
                ("escalate", comp.Escalate ? "true" : "false")));

            if (comp.PayPartialOnFailure && comp.EarnedReward > 0)
            {
                var paid = PayReward(comp, comp.EarnedReward);
                if (paid > 0)
                    message.Append('\n').Append(Loc.GetString("engineering-breakdown-announcement-reward-partial", ("amount", paid)));
            }

            Announce(comp, message.ToString(), comp.FailureSound, comp.FailureColor);
        }

        GameTicker.EndGameRule(uid);
    }

    private int PayReward(EngineeringBreakdownRuleComponent comp, int amount)
    {
        if (amount <= 0 || comp.Station is not { } station || TerminatingOrDeleted(station))
            return 0;

        if (!TryComp<StationBankAccountComponent>(station, out var bank))
            return 0;

        IReadOnlyDictionary<ProtoId<CargoAccountPrototype>, int> accounts = bank.Accounts;
        var account = comp.RewardAccount;
        if (!accounts.ContainsKey(account))
            account = bank.PrimaryAccount;

        _cargo.UpdateBankAccount((station, bank), amount, account);
        Sawmill.Info($"Engineering breakdown reward: {amount} credited to {account} on station {ToPrettyString(station)}");
        return amount;
    }

    private string BuildList(IEnumerable<EntityUid> targets)
    {
        var sb = new StringBuilder();
        foreach (var target in targets)
        {
            if (TerminatingOrDeleted(target) || !TryComp<BreakdownComponent>(target, out var bd))
                continue;

            if (!PrototypeManager.TryIndex(bd.Breakdown, out var proto))
                continue;

            sb.Append('\n');
            sb.Append(Loc.GetString("engineering-breakdown-announcement-entry",
                ("name", Loc.GetString(proto.Name)),
                ("location", FormattedMessage.EscapeText(_breakdown.GetLocation(target)))));
        }

        return sb.ToString();
    }

    private void Announce(EngineeringBreakdownRuleComponent comp, string message, SoundSpecifier? sound, Color color, bool onlyEngineers = false)
    {
        var filter = Filter.Empty().AddWhere(GameTicker.UserHasJoinedGame);
        if (onlyEngineers)
            filter.RemoveWhere(s => s.AttachedEntity is not { } mob || !_breakdown.IsActiveEngineer(mob));

        var sender = Loc.GetString(comp.Sender);
        var wrapped = Loc.GetString("chat-manager-sender-announcement-wrap-message",
            ("sender", sender),
            ("message", message));

        ChatManager.ChatMessageToManyFiltered(filter, ChatChannel.Radio,
            FormattedMessage.RemoveMarkupPermissive(message), wrapped,
            default, false, true, color);

        if (sound != null)
            ChatSystem.SendGlobalSound(sound, filter);
    }
}
