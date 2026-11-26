using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Content.Server.Administration.Logs;
using Content.Shared._BlackM.SecurityCommand;
using Content.Shared.Actions;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.SecurityCommand;

public sealed partial class SecurityCommandSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly ProtoId<DepartmentPrototype> SecurityDepartment = "Security";
    private static readonly TimeSpan StatusCheckInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan PositionsInterval = TimeSpan.FromSeconds(1);

    private readonly Dictionary<int, ServerOrder> _orders = new();
    private readonly Dictionary<int, ServerSquad> _squads = new();
    private readonly Dictionary<EntityUid, int> _membership = new();
    private readonly Dictionary<NetUserId, TimeSpan> _nextRequest = new();

    private int _nextOrderId = 1;
    private int _nextSquadId = 1;
    private bool _dirty;
    private TimeSpan _nextStatusCheck;
    private TimeSpan _nextPositionsUpdate;
    private string _lastOfficerSnapshot = string.Empty;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        SubscribeLocalEvent<SecurityCommanderComponent, MapInitEvent>(OnCommanderMapInit);
        SubscribeLocalEvent<SecurityCommanderComponent, ComponentShutdown>(OnCommanderShutdown);
        SubscribeLocalEvent<SecurityCommanderComponent, OpenSecurityCommandActionEvent>(OnOpenAction);

        SubscribeLocalEvent<SecurityOrderReceiverComponent, ComponentStartup>(OnReceiverStartup);
        SubscribeLocalEvent<SecurityOrderReceiverComponent, MapInitEvent>(OnReceiverMapInit);
        SubscribeLocalEvent<SecurityOrderReceiverComponent, OpenSecurityMapActionEvent>(OnOpenMapAction);
        SubscribeLocalEvent<SecurityOrderReceiverComponent, ComponentShutdown>(OnReceiverShutdown);
        SubscribeLocalEvent<SecurityOrderReceiverComponent, PlayerAttachedEvent>(OnReceiverAttached);
        SubscribeLocalEvent<SecurityOrderReceiverComponent, MobStateChangedEvent>(OnReceiverMobState);
        SubscribeLocalEvent<SecurityOrderReceiverComponent, MoveEvent>(OnPostOfficerMoved);

        SubscribeNetworkEvent<SecurityCommandCreateOrderMessage>(OnCreateOrder);
        SubscribeNetworkEvent<SecurityCommandRemoveOrderMessage>(OnRemoveOrder);
        SubscribeNetworkEvent<SecurityCommandClearOrdersMessage>(OnClearOrders);
        SubscribeNetworkEvent<SecurityCommandCreateSquadMessage>(OnCreateSquad);
        SubscribeNetworkEvent<SecurityCommandRemoveSquadMessage>(OnRemoveSquad);
        SubscribeNetworkEvent<SecurityCommandAssignOfficerMessage>(OnAssignOfficer);
    }

    private void OnSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId == null)
            return;

        if (!_proto.TryIndex(SecurityDepartment, out var department))
            return;

        if (!department.Roles.Contains(ev.JobId))
            return;

        EnsureComp<SecurityOrderReceiverComponent>(ev.Mob);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _orders.Clear();
        _squads.Clear();
        _membership.Clear();
        _nextRequest.Clear();
        _nextOrderId = 1;
        _nextSquadId = 1;
        _nextPostCheck = TimeSpan.Zero;
        _lastOfficerSnapshot = string.Empty;
        _dirty = false;
    }

    private void OnCommanderMapInit(Entity<SecurityCommanderComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.ActionEntity, ent.Comp.Action);
        var receiver = EnsureComp<SecurityOrderReceiverComponent>(ent.Owner);

        if (receiver.MapActionEntity != null)
        {
            _actions.RemoveAction(ent.Owner, receiver.MapActionEntity);
            receiver.MapActionEntity = null;
        }

        _dirty = true;
    }

    private void OnCommanderShutdown(Entity<SecurityCommanderComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);

        if (!TerminatingOrDeleted(ent.Owner) && TryComp<SecurityOrderReceiverComponent>(ent.Owner, out var receiver))
            _actions.AddAction(ent.Owner, ref receiver.MapActionEntity, receiver.MapAction);

        _dirty = true;
    }

    private void OnReceiverMapInit(Entity<SecurityOrderReceiverComponent> ent, ref MapInitEvent args)
    {
        if (HasComp<SecurityCommanderComponent>(ent.Owner))
            return;

        _actions.AddAction(ent.Owner, ref ent.Comp.MapActionEntity, ent.Comp.MapAction);
    }

    private void OnReceiverStartup(Entity<SecurityOrderReceiverComponent> ent, ref ComponentStartup args)
    {
        _dirty = true;
    }

    private void OnReceiverShutdown(Entity<SecurityOrderReceiverComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.MapActionEntity);
        ent.Comp.MapActionEntity = null;
        RemoveFromSquad(ent.Owner);
        foreach (var order in _orders.Values)
        {
            order.PostDuties?.Remove(ent.Owner);
        }

        var toRemove = _orders.Values
            .Where(o => o.TargetKind == SecurityOrderTargetKind.Officer && o.Officer == ent.Owner)
            .Select(o => o.Id)
            .ToList();

        foreach (var id in toRemove)
        {
            _orders.Remove(id);
        }

        _dirty = true;
    }

    private void OnReceiverAttached(Entity<SecurityOrderReceiverComponent> ent, ref PlayerAttachedEvent args)
    {
        _dirty = true;
    }

    private void OnReceiverMobState(Entity<SecurityOrderReceiverComponent> ent, ref MobStateChangedEvent args)
    {
        _dirty = true;

        if (args.OldMobState != MobState.Alive || args.NewMobState is not (MobState.Critical or MobState.Dead))
            return;

        var commanders = EntityQueryEnumerator<SecurityCommanderComponent, ActorComponent>();
        while (commanders.MoveNext(out var uid, out _, out var actor))
        {
            if (uid == ent.Owner)
                continue;

            _audio.PlayGlobal(SecurityCommandSounds.OfficerDown, actor.PlayerSession, AudioParams.Default.WithVolume(-9f));
        }
    }

    private void OnOpenAction(Entity<SecurityCommanderComponent> ent, ref OpenSecurityCommandActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<ActorComponent>(ent, out var actor))
            return;

        if (_mobState.IsIncapacitated(ent))
        {
            _popup.PopupEntity(Loc.GetString("security-command-popup-incapacitated"), ent, ent);
            return;
        }

        args.Handled = true;

        RaiseNetworkEvent(BuildState(ent, true, BuildOfficers()), actor.PlayerSession);
        RaiseNetworkEvent(new SecurityCommandOpenUiMessage(), actor.PlayerSession);
    }

    private void OnOpenMapAction(Entity<SecurityOrderReceiverComponent> ent, ref OpenSecurityMapActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<ActorComponent>(ent, out var actor))
            return;

        if (_mobState.IsIncapacitated(ent))
        {
            _popup.PopupEntity(Loc.GetString("security-command-popup-incapacitated"), ent, ent);
            return;
        }

        args.Handled = true;

        var commander = HasComp<SecurityCommanderComponent>(ent);
        RaiseNetworkEvent(BuildState(ent, commander, BuildOfficers()), actor.PlayerSession);
        RaiseNetworkEvent(new SecurityCommandOpenUiMessage(), actor.PlayerSession);
    }

    private bool TryGetCommander(EntitySessionEventArgs args, out EntityUid commander, bool rateLimited = true)
    {
        commander = default;

        if (args.SenderSession.AttachedEntity is not { } user)
            return false;

        if (!HasComp<SecurityCommanderComponent>(user) || _mobState.IsIncapacitated(user))
            return false;

        if (rateLimited)
        {
            var now = _timing.CurTime;
            if (_nextRequest.TryGetValue(args.SenderSession.UserId, out var next) && now < next)
                return false;

            _nextRequest[args.SenderSession.UserId] = now + SecurityCommandConstants.RequestCooldown;
        }

        commander = user;
        return true;
    }

    private void OnCreateOrder(SecurityCommandCreateOrderMessage msg, EntitySessionEventArgs args)
    {
        if (!TryGetCommander(args, out var commander))
            return;

        if (!Enum.IsDefined(typeof(SecurityOrderType), msg.Type) ||
            !Enum.IsDefined(typeof(SecurityOrderTargetKind), msg.TargetKind))
            return;

        if (_orders.Count >= SecurityCommandConstants.MaxOrders)
        {
            _popup.PopupEntity(Loc.GetString("security-command-popup-too-many-orders"), commander, commander);
            return;
        }

        if (!SecurityCommandConstants.IsValidPointCount(msg.Type, msg.Points.Count))
            return;

        var postMinutes = msg.Type == SecurityOrderType.Post ? msg.PostDurationMinutes : 0;
        if (postMinutes is < 0 or > SecurityCommandConstants.MaxPostDurationMinutes)
            return;

        var text = SecurityCommandConstants.SanitizeText(msg.Text, SecurityCommandConstants.MaxTextLength);
        if (SecurityCommandConstants.RequiresText(msg.Type) && text.Length == 0)
        {
            _popup.PopupEntity(Loc.GetString("security-command-popup-need-text"), commander, commander);
            return;
        }

        var commanderMap = Transform(commander).MapID;
        var points = new List<EntityCoordinates>(msg.Points.Count);
        foreach (var netCoords in msg.Points)
        {
            if (!TryGetEntity(netCoords.NetEntity, out _))
                return;

            var coords = GetCoordinates(netCoords);
            if (!coords.IsValid(EntityManager))
                return;

            if (_transform.ToMapCoordinates(coords).MapId != commanderMap)
                return;

            points.Add(coords);
        }

        if (postMinutes > 0 && !TryGetPostArea(points, out _))
        {
            _popup.PopupEntity(Loc.GetString("security-command-post-invalid-area"), commander, commander);
            return;
        }

        EntityUid? officer = null;
        var squadId = 0;
        switch (msg.TargetKind)
        {
            case SecurityOrderTargetKind.Squad:
                if (!_squads.ContainsKey(msg.SquadId))
                    return;
                squadId = msg.SquadId;
                break;
            case SecurityOrderTargetKind.Officer:
                if (!TryGetEntity(msg.Officer, out officer) || !HasComp<SecurityOrderReceiverComponent>(officer))
                    return;
                break;
        }

        var color = msg.Color.WithAlpha(1f);

        var order = new ServerOrder
        {
            Id = _nextOrderId++,
            Type = msg.Type,
            Points = points,
            Text = text,
            Color = color,
            TargetKind = msg.TargetKind,
            SquadId = squadId,
            Officer = officer,
            IssuerName = Name(commander),
            IssuedAt = _timing.CurTime,
            PostDurationMinutes = postMinutes,
            PostDuties = postMinutes > 0 ? new() : null,
        };

        _orders[order.Id] = order;

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(commander):user} issued security order #{order.Id} {order.Type} '{order.Text}' to {DescribeTarget(order)}");

        var typeName = Loc.GetString(SecurityCommandConstants.GetTypeLoc(order.Type));
        var notice = order.Text.Length > 0
            ? Loc.GetString("security-command-popup-new-order", ("type", typeName), ("text", order.Text))
            : Loc.GetString("security-command-popup-new-order-notext", ("type", typeName));
        var (sound, volume) = order.Type == SecurityOrderType.Danger
            ? (SecurityCommandSounds.DangerOrder, -11f)
            : (SecurityCommandSounds.NewOrder, -8f);
        NotifyRecipients(order, notice, sound, volume, commander);

        _dirty = true;
    }

    private void OnRemoveOrder(SecurityCommandRemoveOrderMessage msg, EntitySessionEventArgs args)
    {
        if (!TryGetCommander(args, out var commander, false))
            return;

        if (!_orders.Remove(msg.OrderId, out var order))
            return;

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(commander):user} cancelled security order #{order.Id} {order.Type} '{order.Text}'");

        NotifyRecipients(order, Loc.GetString("security-command-popup-order-cancelled",
            ("type", Loc.GetString(SecurityCommandConstants.GetTypeLoc(order.Type)))),
            SecurityCommandSounds.OrderCancelled, -10f, commander);

        _dirty = true;
    }

    private void OnClearOrders(SecurityCommandClearOrdersMessage msg, EntitySessionEventArgs args)
    {
        if (!TryGetCommander(args, out var commander))
            return;

        if (_orders.Count == 0)
            return;

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(commander):user} cleared all {_orders.Count} security orders");

        _orders.Clear();

        var query = EntityQueryEnumerator<SecurityOrderReceiverComponent, ActorComponent>();
        while (query.MoveNext(out var uid, out _, out var actor))
        {
            _popup.PopupEntity(Loc.GetString("security-command-popup-orders-cleared"), uid, uid);

            if (uid != commander)
                _audio.PlayGlobal(SecurityCommandSounds.OrderCancelled, actor.PlayerSession, AudioParams.Default.WithVolume(-10f));
        }

        _dirty = true;
    }

    private void OnCreateSquad(SecurityCommandCreateSquadMessage msg, EntitySessionEventArgs args)
    {
        if (!TryGetCommander(args, out var commander))
            return;

        if (_squads.Count >= SecurityCommandConstants.MaxSquads)
        {
            _popup.PopupEntity(Loc.GetString("security-command-popup-too-many-squads"), commander, commander);
            return;
        }

        var id = _nextSquadId++;
        var name = SecurityCommandConstants.SanitizeText(msg.Name, SecurityCommandConstants.MaxSquadNameLength);
        if (name.Length == 0)
            name = Loc.GetString("security-command-squad-default-name", ("number", id));

        _squads[id] = new ServerSquad
        {
            Id = id,
            Name = name,
            Color = msg.Color.WithAlpha(1f),
        };

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(commander):user} created security squad #{id} '{name}'");

        _dirty = true;
    }

    private void OnRemoveSquad(SecurityCommandRemoveSquadMessage msg, EntitySessionEventArgs args)
    {
        if (!TryGetCommander(args, out var commander, false))
            return;

        if (!_squads.Remove(msg.SquadId, out var squad))
            return;

        foreach (var (member, squadId) in _membership.ToList())
        {
            if (squadId != msg.SquadId)
                continue;

            _membership.Remove(member);
            if (TryComp<ActorComponent>(member, out var memberActor))
            {
                _popup.PopupEntity(Loc.GetString("security-command-popup-squad-disbanded", ("squad", squad.Name)), member, member);
                _audio.PlayGlobal(SecurityCommandSounds.Unassigned, memberActor.PlayerSession, AudioParams.Default.WithVolume(-10f));
            }
        }

        var orphaned = _orders.Values
            .Where(o => o.TargetKind == SecurityOrderTargetKind.Squad && o.SquadId == msg.SquadId)
            .Select(o => o.Id)
            .ToList();

        foreach (var id in orphaned)
        {
            _orders.Remove(id);
        }

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(commander):user} disbanded security squad #{squad.Id} '{squad.Name}'");

        _dirty = true;
    }

    private void OnAssignOfficer(SecurityCommandAssignOfficerMessage msg, EntitySessionEventArgs args)
    {
        if (!TryGetCommander(args, out _, false))
            return;

        if (!TryGetEntity(msg.Officer, out var officer) || !HasComp<SecurityOrderReceiverComponent>(officer))
            return;

        var uid = officer.Value;

        if (msg.SquadId == 0)
        {
            if (RemoveFromSquad(uid) && TryComp<ActorComponent>(uid, out var unassignedActor))
            {
                _popup.PopupEntity(Loc.GetString("security-command-popup-unassigned"), uid, uid);
                _audio.PlayGlobal(SecurityCommandSounds.Unassigned, unassignedActor.PlayerSession, AudioParams.Default.WithVolume(-10f));
            }

            _dirty = true;
            return;
        }

        if (!_squads.TryGetValue(msg.SquadId, out var squad))
            return;

        if (_membership.TryGetValue(uid, out var current) && current == msg.SquadId)
            return;

        _membership[uid] = msg.SquadId;

        if (TryComp<ActorComponent>(uid, out var assignedActor))
        {
            _popup.PopupEntity(Loc.GetString("security-command-popup-assigned", ("squad", squad.Name)), uid, uid, PopupType.Medium);
            _audio.PlayGlobal(SecurityCommandSounds.Assigned, assignedActor.PlayerSession, AudioParams.Default.WithVolume(-9f));
        }

        _dirty = true;
    }

    private bool RemoveFromSquad(EntityUid uid)
    {
        return _membership.Remove(uid);
    }

    private int GetSquad(EntityUid uid)
    {
        return _membership.TryGetValue(uid, out var id) ? id : 0;
    }

    private bool IsRelevant(ServerOrder order, EntityUid receiver)
    {
        return order.TargetKind switch
        {
            SecurityOrderTargetKind.All => true,
            SecurityOrderTargetKind.Squad => GetSquad(receiver) == order.SquadId,
            SecurityOrderTargetKind.Officer => order.Officer == receiver,
            _ => false,
        };
    }

    private void NotifyRecipients(ServerOrder order, string message, SoundSpecifier? sound, float volume, EntityUid? except = null)
    {
        var query = EntityQueryEnumerator<SecurityOrderReceiverComponent, ActorComponent>();
        while (query.MoveNext(out var uid, out _, out var actor))
        {
            if (!IsRelevant(order, uid))
                continue;

            _popup.PopupEntity(message, uid, uid, PopupType.Medium);

            if (sound != null && uid != except)
                _audio.PlayGlobal(sound, actor.PlayerSession, AudioParams.Default.WithVolume(volume));
        }
    }

    private string DescribeTarget(ServerOrder order)
    {
        return order.TargetKind switch
        {
            SecurityOrderTargetKind.Squad => _squads.TryGetValue(order.SquadId, out var squad)
                ? $"squad '{squad.Name}'"
                : "unknown squad",
            SecurityOrderTargetKind.Officer => order.Officer is { } officer ? ToPrettyString(officer).ToString() : "unknown officer",
            _ => "all security",
        };
    }

    private SecurityOfficerStatus GetStatus(EntityUid uid)
    {
        if (_mobState.IsDead(uid))
            return SecurityOfficerStatus.Dead;

        if (_mobState.IsCritical(uid))
            return SecurityOfficerStatus.Critical;

        return HasComp<ActorComponent>(uid) ? SecurityOfficerStatus.Alive : SecurityOfficerStatus.Offline;
    }

    private List<SecurityOfficerData> BuildOfficers()
    {
        var list = new List<SecurityOfficerData>();
        var query = EntityQueryEnumerator<SecurityOrderReceiverComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            var job = string.Empty;
            var jobId = string.Empty;
            if (_mind.TryGetMind(uid, out var mindId, out _))
            {
                _jobs.MindTryGetJobName(mindId, out job);
                if (_jobs.MindTryGetJobId(mindId, out var jobProto) && jobProto != null)
                    jobId = jobProto.Value.Id;
            }

            list.Add(new SecurityOfficerData
            {
                Entity = GetNetEntity(uid),
                Name = Name(uid),
                Job = job,
                JobId = jobId,
                SquadId = GetSquad(uid),
                Status = GetStatus(uid),
                IsCommander = HasComp<SecurityCommanderComponent>(uid),
            });
        }

        list.Sort((a, b) =>
        {
            var cmd = b.IsCommander.CompareTo(a.IsCommander);
            return cmd != 0 ? cmd : string.Compare(a.Name, b.Name, StringComparison.CurrentCulture);
        });

        return list;
    }

    private SecurityOrderData ToData(ServerOrder order, EntityUid viewer)
    {
        var data = new SecurityOrderData
        {
            Id = order.Id,
            Type = order.Type,
            Text = order.Text,
            Color = order.Color,
            TargetKind = order.TargetKind,
            SquadId = order.SquadId,
            Officer = order.Officer is { } officer ? GetNetEntity(officer) : null,
            IssuerName = order.IssuerName,
            IssuedAt = order.IssuedAt,
            PostDurationMinutes = order.PostDurationMinutes,
        };

        if (order.PostDuties != null && order.PostDuties.TryGetValue(viewer, out var duty))
        {
            data.PostDutyStatus = duty.Status;
            data.PostDutyEndsAt = duty.EndsAt;
        }

        foreach (var point in order.Points)
        {
            data.Points.Add(GetNetCoordinates(point));
        }

        return data;
    }

    private SecurityCommandStateMessage BuildState(EntityUid viewer, bool commander, List<SecurityOfficerData> officers)
    {
        var state = new SecurityCommandStateMessage
        {
            IsCommander = commander,
            OwnSquadId = GetSquad(viewer),
        };

        foreach (var order in _orders.Values.OrderBy(o => o.Id))
        {
            if (commander || IsRelevant(order, viewer))
                state.Orders.Add(ToData(order, viewer));
        }

        foreach (var squad in _squads.Values.OrderBy(s => s.Id))
        {
            var data = new SecuritySquadData
            {
                Id = squad.Id,
                Name = squad.Name,
                Color = squad.Color,
            };

            if (commander)
            {
                foreach (var (member, squadId) in _membership)
                {
                    if (squadId == squad.Id && !TerminatingOrDeleted(member))
                        data.Members.Add(GetNetEntity(member));
                }
            }

            state.Squads.Add(data);
        }

        if (commander)
        {
            state.Officers = officers;
        }
        else
        {
            var self = GetNetEntity(viewer);
            var own = state.OwnSquadId;
            state.Officers = officers.Where(o => o.Entity == self || own != 0 && o.SquadId == own).ToList();
        }

        return state;
    }

    private void PruneInvalidOrders()
    {
        List<int>? invalid = null;
        foreach (var order in _orders.Values)
        {
            foreach (var point in order.Points)
            {
                if (point.IsValid(EntityManager))
                    continue;

                invalid ??= new List<int>();
                invalid.Add(order.Id);
                break;
            }
        }

        if (invalid == null)
            return;

        foreach (var id in invalid)
        {
            _orders.Remove(id);
        }
    }

    private string BuildOfficerSnapshot(List<SecurityOfficerData> officers)
    {
        var sb = new StringBuilder();
        foreach (var o in officers)
        {
            sb.Append(o.Entity.Id).Append(':')
                .Append((byte) o.Status).Append(':')
                .Append(o.SquadId).Append(':')
                .Append(o.Name).Append(':')
                .Append(o.Job).Append(':')
                .Append(o.JobId).Append(';');
        }

        return sb.ToString();
    }

    private void BroadcastState(List<SecurityOfficerData> officers)
    {
        PruneInvalidOrders();

        var query = EntityQueryEnumerator<SecurityOrderReceiverComponent, ActorComponent>();
        while (query.MoveNext(out var uid, out _, out var actor))
        {
            var commander = HasComp<SecurityCommanderComponent>(uid);
            RaiseNetworkEvent(BuildState(uid, commander, officers), actor.PlayerSession);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        List<SecurityOfficerData>? officers = null;
        UpdatePostDuties(now);

        if (now >= _nextPositionsUpdate)
        {
            _nextPositionsUpdate = now + PositionsInterval;
            BroadcastPositions();
        }

        if (now >= _nextStatusCheck)
        {
            _nextStatusCheck = now + StatusCheckInterval;
            officers = BuildOfficers();
            var snapshot = BuildOfficerSnapshot(officers);
            if (snapshot != _lastOfficerSnapshot)
            {
                _lastOfficerSnapshot = snapshot;
                _dirty = true;
            }
        }

        if (!_dirty)
            return;

        _dirty = false;
        officers ??= BuildOfficers();
        _lastOfficerSnapshot = BuildOfficerSnapshot(officers);
        BroadcastState(officers);
    }

    private void BroadcastPositions()
    {
        var all = new List<(EntityUid Uid, SecurityOfficerPosition Pos)>();
        var query = EntityQueryEnumerator<SecurityOrderReceiverComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapID == MapId.Nullspace)
                continue;

            all.Add((uid, new SecurityOfficerPosition
            {
                Entity = GetNetEntity(uid),
                Coordinates = GetNetCoordinates(_transform.GetMoverCoordinates(uid, xform)),
            }));
        }

        if (all.Count == 0)
            return;

        SecurityCommandPositionsMessage? full = null;
        var squadMessages = new Dictionary<int, SecurityCommandPositionsMessage>();

        var receivers = EntityQueryEnumerator<SecurityOrderReceiverComponent, ActorComponent>();
        while (receivers.MoveNext(out var uid, out _, out var actor))
        {
            if (HasComp<SecurityCommanderComponent>(uid))
            {
                if (full == null)
                {
                    full = new SecurityCommandPositionsMessage();
                    foreach (var (_, pos) in all)
                    {
                        full.Positions.Add(pos);
                    }
                }

                RaiseNetworkEvent(full, actor.PlayerSession);
                continue;
            }

            var squad = GetSquad(uid);
            if (squad == 0)
                continue;

            if (!squadMessages.TryGetValue(squad, out var msg))
            {
                msg = new SecurityCommandPositionsMessage();
                foreach (var (member, pos) in all)
                {
                    if (GetSquad(member) == squad)
                        msg.Positions.Add(pos);
                }

                squadMessages[squad] = msg;
            }

            RaiseNetworkEvent(msg, actor.PlayerSession);
        }
    }

    private sealed class ServerOrder
    {
        public int Id;
        public SecurityOrderType Type;
        public List<EntityCoordinates> Points = new();
        public string Text = string.Empty;
        public Color Color = Color.White;
        public SecurityOrderTargetKind TargetKind;
        public int SquadId;
        public EntityUid? Officer;
        public string IssuerName = string.Empty;
        public TimeSpan IssuedAt;
        public int PostDurationMinutes;
        public Dictionary<EntityUid, PostDuty>? PostDuties;
    }

    private sealed class ServerSquad
    {
        public int Id;
        public string Name = string.Empty;
        public Color Color = Color.White;
    }
}
