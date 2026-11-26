using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Server.Chat.Managers;
using Content.Shared._BlackM.SecurityCommand;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._BlackM.SecurityCommand;

public sealed partial class SecurityCommandSystem
{
    [Dependency] private readonly IChatManager _chat = default!;

    private static readonly ProtoId<JobPrototype> HeadOfSecurity = "HeadOfSecurity";
    private static readonly TimeSpan PostCheckInterval = TimeSpan.FromSeconds(0.25);
    private TimeSpan _nextPostCheck;

    private readonly record struct PostDuty(SecurityPostDutyStatus Status, TimeSpan EndsAt);

    private readonly record struct PostArea(MapId Map, Box2 Bounds, Matrix3x2 WorldToArea)
    {
        public bool Contains(MapCoordinates coordinates)
        {
            return coordinates.MapId == Map &&
                   Bounds.Contains(Vector2.Transform(coordinates.Position, WorldToArea));
        }
    }

    private void UpdatePostDuties(TimeSpan now)
    {
        if (now < _nextPostCheck)
            return;

        _nextPostCheck = now + PostCheckInterval;

        foreach (var order in _orders.Values)
        {
            if (order.PostDuties is not { } duties || !TryGetPostArea(order.Points, out var area))
                continue;

            var officers = EntityQueryEnumerator<SecurityOrderReceiverComponent, TransformComponent>();
            while (officers.MoveNext(out var officer, out _, out var transform))
            {
                if (!IsRelevant(order, officer))
                {
                    if (duties.Remove(officer))
                        _dirty = true;

                    continue;
                }

                var duty = duties.GetValueOrDefault(officer);
                if (duty.Status is SecurityPostDutyStatus.Waiting or SecurityPostDutyStatus.Serving)
                    UpdatePostDuty(order, duties, officer, transform, area, duty, now);
            }
        }
    }

    private void OnPostOfficerMoved(Entity<SecurityOrderReceiverComponent> ent, ref MoveEvent args)
    {
        foreach (var order in _orders.Values)
        {
            if (order.PostDuties is not { } duties ||
                !duties.TryGetValue(ent.Owner, out var duty) || duty.Status != SecurityPostDutyStatus.Serving ||
                !IsRelevant(order, ent.Owner) || !TryGetPostArea(order.Points, out var area))
                continue;

            UpdatePostDuty(order, duties, ent.Owner, Transform(ent.Owner), area, duty, _timing.CurTime);
        }
    }

    private void UpdatePostDuty(ServerOrder order, Dictionary<EntityUid, PostDuty> duties, EntityUid officer,
        TransformComponent transform, PostArea area, PostDuty duty, TimeSpan now)
    {
        if (duty.Status == SecurityPostDutyStatus.Serving && now >= duty.EndsAt)
        {
            duties[officer] = new PostDuty(SecurityPostDutyStatus.Completed, default);
            if (HasComp<ActorComponent>(officer))
                _popup.PopupEntity(Loc.GetString("security-command-post-completed"), officer, officer);

            _dirty = true;
            return;
        }

        var onDuty = HasComp<ActorComponent>(officer) && !_mobState.IsIncapacitated(officer) &&
                     area.Contains(_transform.GetMapCoordinates(officer, transform));
        if (onDuty && duty.Status == SecurityPostDutyStatus.Waiting)
        {
            duties[officer] = new PostDuty(SecurityPostDutyStatus.Serving,
                now + TimeSpan.FromMinutes(order.PostDurationMinutes));
            _dirty = true;
        }
        else if (!onDuty && duty.Status == SecurityPostDutyStatus.Serving)
        {
            duties[officer] = new PostDuty(SecurityPostDutyStatus.LeftEarly, default);
            _dirty = true;
            ReportEarlyDeparture(order, officer, duty.EndsAt - now);
        }
    }

    private void ReportEarlyDeparture(ServerOrder order, EntityUid officer, TimeSpan remaining)
    {
        var postName = order.Text.Length > 0 ? order.Text : Loc.GetString("security-command-type-post");
        var message = Loc.GetString("security-command-post-left-early-notice",
            ("officer", Name(officer)),
            ("post", postName),
            ("time", SecurityCommandConstants.FormatPostTime(remaining)));

        var actors = EntityQueryEnumerator<ActorComponent>();
        while (actors.MoveNext(out var head, out var actor))
        {
            if (!_mind.TryGetMind(head, out var mind, out _) ||
                !_jobs.MindTryGetJobId(mind, out var job) || job != HeadOfSecurity)
                continue;

            _chat.DispatchServerMessage(actor.PlayerSession, message);
            _popup.PopupEntity(message, head, head, PopupType.MediumCaution);
            _audio.PlayGlobal(SecurityCommandSounds.PostInterrupted, actor.PlayerSession, AudioParams.Default.WithVolume(-13f));
        }
    }

    private bool TryGetPostArea(List<EntityCoordinates> points, out PostArea area)
    {
        area = default;
        var first = points[0];
        var second = points[1];
        if (!first.IsValid(EntityManager) || !second.IsValid(EntityManager))
            return false;

        var firstMap = _transform.ToMapCoordinates(first);
        var secondMap = _transform.ToMapCoordinates(second);
        if (firstMap.MapId == MapId.Nullspace || firstMap.MapId != secondMap.MapId)
            return false;

        var sharedAnchor = first.EntityId == second.EntityId;
        var a = sharedAnchor ? first.Position : firstMap.Position;
        var b = sharedAnchor ? second.Position : secondMap.Position;
        if (a.X == b.X || a.Y == b.Y)
            return false;

        var inverse = sharedAnchor ? _transform.GetInvWorldMatrix(first.EntityId) : Matrix3x2.Identity;
        area = new PostArea(firstMap.MapId, new Box2(Vector2.Min(a, b), Vector2.Max(a, b)), inverse);
        return true;
    }
}
