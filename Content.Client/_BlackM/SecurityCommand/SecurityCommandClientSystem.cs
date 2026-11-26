using System;
using System.Collections.Generic;
using Content.Client.Construction;
using Content.Client.ContextMenu.UI;
using Content.Client.Interaction;
using Content.Shared._BlackM.SecurityCommand;
using Content.Shared.Interaction;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using static Robust.Shared.Input.Binding.PointerInputCmdHandler;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._BlackM.SecurityCommand;

public sealed class SecurityCommandClientSystem : EntitySystem
{
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly InputSystem _inputSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public SecurityCommandStateMessage? State { get; private set; }

    public readonly Dictionary<NetEntity, NetCoordinates> OfficerPositions = new();

    public SecurityOrderType? PlacingType { get; private set; }

    public SecurityOrderDraft Draft { get; private set; }

    public readonly List<EntityCoordinates> PendingPoints = new();

    public bool WindowOpen => _window is { IsOpen: true };

    public event Action? StateUpdated;
    public event Action? PlacementChanged;

    private SecurityCommandWindow? _window;
    private SecurityCommandOverlay? _overlay;
    private SecurityCommandPixel? _pixel;

    public SecurityCommandPixel Pixel => _pixel ??= new SecurityCommandPixel(_sprite);

    private const float MarkerFadeTime = 0.6f;

    private readonly Dictionary<int, TimeSpan> _reachedOrders = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<SecurityCommandStateMessage>(OnState);
        SubscribeNetworkEvent<SecurityCommandPositionsMessage>(OnPositions);
        SubscribeNetworkEvent<SecurityCommandOpenUiMessage>(OnOpenUi);
        SubscribeLocalEvent<SecurityCommanderComponent, ComponentShutdown>(OnCommanderShutdown);

        _player.LocalPlayerDetached += OnLocalDetached;

        _overlay = new SecurityCommandOverlay(this);
        _overlayManager.AddOverlay(_overlay);

        CommandBinds.Builder
            .BindBefore(EngineKeyFunctions.Use,
                new PointerInputCmdHandler(OnUse, outsidePrediction: true),
                typeof(ConstructionSystem), typeof(DragDropSystem), typeof(SharedInteractionSystem))
            .BindBefore(EngineKeyFunctions.UseSecondary,
                new PointerInputCmdHandler(OnUseSecondary, outsidePrediction: true),
                typeof(EntityMenuUIController))
            .Register<SecurityCommandClientSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();

        CommandBinds.Unregister<SecurityCommandClientSystem>();
        _player.LocalPlayerDetached -= OnLocalDetached;

        if (_overlay != null)
            _overlayManager.RemoveOverlay(_overlay);

        _window?.Close();
        _window = null;
    }

    private void OnState(SecurityCommandStateMessage msg)
    {
        State = msg;
        PruneReached(msg);

        if (!msg.IsCommander)
            CancelPlacement();

        StateUpdated?.Invoke();
    }

    private void OnPositions(SecurityCommandPositionsMessage msg)
    {
        OfficerPositions.Clear();
        foreach (var pos in msg.Positions)
        {
            OfficerPositions[pos.Entity] = pos.Coordinates;
        }
    }

    public bool TryGetOfficerPosition(NetEntity officer, out MapCoordinates position)
    {
        position = MapCoordinates.Nullspace;

        if (TryGetEntity(officer, out var uid) && TryComp(uid, out TransformComponent? xform) &&
            xform.MapID != MapId.Nullspace)
        {
            position = _transform.GetMapCoordinates(uid.Value, xform);
            return true;
        }

        if (!OfficerPositions.TryGetValue(officer, out var net))
            return false;

        var coords = GetCoordinates(net);
        if (!coords.IsValid(EntityManager))
            return false;

        position = _transform.ToMapCoordinates(coords);
        return position.MapId != MapId.Nullspace;
    }

    public Texture GetIcon(SpriteSpecifier specifier)
    {
        return _sprite.Frame0(specifier);
    }

    private void OnOpenUi(SecurityCommandOpenUiMessage msg)
    {
        if (_window == null)
        {
            _window = new SecurityCommandWindow(this);
            _window.OnClose += CancelPlacement;
        }

        PlayUi(SecurityCommandSounds.UiToggle, -3f);

        if (_window.IsOpen)
        {
            _window.Close();
            return;
        }

        _window.OpenCenteredLeft();
    }

    private void OnCommanderShutdown(Entity<SecurityCommanderComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Owner != _player.LocalEntity)
            return;

        CancelPlacement();
        _window?.Close();
    }

    private void OnLocalDetached(EntityUid uid)
    {
        CancelPlacement();
        _window?.Close();
        State = null;
        _reachedOrders.Clear();
        StateUpdated?.Invoke();
    }

    public static bool HidesOnArrival(SecurityOrderType type)
    {
        return type is SecurityOrderType.Move;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (State == null || _player.LocalEntity is not { } local || !TryComp(local, out TransformComponent? xform))
            return;

        var position = _transform.GetMapCoordinates(local, xform);
        if (position.MapId == MapId.Nullspace)
            return;

        foreach (var order in State.Orders)
        {
            if (!HidesOnArrival(order.Type) || order.Points.Count == 0 || _reachedOrders.ContainsKey(order.Id))
                continue;

            if (!IsRelevantToLocal(order))
                continue;

            var coords = GetCoordinates(order.Points[0]);
            if (!coords.IsValid(EntityManager))
                continue;

            var map = _transform.ToMapCoordinates(coords);
            if (map.MapId != position.MapId)
                continue;

            if ((map.Position - position.Position).Length() <= SecurityCommandConstants.ArrivalDistance)
            {
                _reachedOrders[order.Id] = _timing.RealTime;
                PlayUi(SecurityCommandSounds.Arrival, -2f);
            }
        }
    }

    public float GetMarkerFade(SecurityOrderData order)
    {
        if (!_reachedOrders.TryGetValue(order.Id, out var at))
            return 1f;

        var t = (float) (_timing.RealTime - at).TotalSeconds / MarkerFadeTime;
        return Math.Clamp(1f - t, 0f, 1f);
    }

    public bool IsReached(int orderId)
    {
        return _reachedOrders.ContainsKey(orderId);
    }

    private void PruneReached(SecurityCommandStateMessage msg)
    {
        if (_reachedOrders.Count == 0)
            return;

        var alive = new HashSet<int>();
        foreach (var order in msg.Orders)
        {
            alive.Add(order.Id);
        }

        foreach (var id in new List<int>(_reachedOrders.Keys))
        {
            if (!alive.Contains(id))
                _reachedOrders.Remove(id);
        }
    }

    public void StartPlacement(SecurityOrderType type, SecurityOrderDraft draft)
    {
        PlacingType = type;
        Draft = draft;
        PendingPoints.Clear();
        PlayUi(SecurityCommandSounds.ToolSelect, -3f);
        PlacementChanged?.Invoke();
    }

    public void SetPostDuration(int minutes)
    {
        if (PlacingType == SecurityOrderType.Post)
            Draft = Draft with { PostDurationMinutes = minutes };
    }

    public void CancelPlacement()
    {
        if (PlacingType != null)
            PlayUi(SecurityCommandSounds.PlacementCancel, -11f);

        ResetPlacement();
    }

    private void ResetPlacement()
    {
        if (PlacingType == null && PendingPoints.Count == 0)
            return;

        PlacingType = null;
        PendingPoints.Clear();
        PlacementChanged?.Invoke();
    }

    public bool FinishPlacement()
    {
        return FinishPlacement(true);
    }

    private bool FinishPlacement(bool feedback)
    {
        if (PlacingType is not { } type)
            return false;

        if (!SecurityCommandConstants.IsValidPointCount(type, PendingPoints.Count))
        {
            if (feedback)
                PlayUi(SecurityCommandSounds.Invalid, -16f);

            return false;
        }

        var msg = new SecurityCommandCreateOrderMessage
        {
            Type = type,
            Text = Draft.Text,
            Color = Draft.Color,
            TargetKind = Draft.TargetKind,
            SquadId = Draft.SquadId,
            Officer = Draft.Officer,
            PostDurationMinutes = Draft.PostDurationMinutes,
        };

        foreach (var point in PendingPoints)
        {
            msg.Points.Add(GetNetCoordinates(point));
        }

        RaiseNetworkEvent(msg);
        PlayUi(SecurityCommandSounds.OrderSent, -2f);
        ResetPlacement();
        return true;
    }

    private void AddPoint(EntityCoordinates coords)
    {
        if (PlacingType is not { } type)
            return;

        PendingPoints.Add(coords);

        var done = type switch
        {
            SecurityOrderType.PatrolRoute => PendingPoints.Count >= SecurityCommandConstants.MaxRoutePoints,
            SecurityOrderType.PatrolZone or SecurityOrderType.Post => PendingPoints.Count >= 2,
            _ => true,
        };

        if (done)
        {
            FinishPlacement();
            return;
        }

        PlayUi(SecurityCommandSounds.PointPlaced, -10f, 1f + 0.025f * Math.Min(PendingPoints.Count - 1, 10));
        PlacementChanged?.Invoke();
    }

    public bool AddMapPoint(EntityCoordinates coords)
    {
        if (PlacingType == null)
            return false;

        if (State is not { IsCommander: true })
        {
            CancelPlacement();
            return false;
        }

        if (!coords.IsValid(EntityManager))
            return false;

        AddPoint(coords);
        return true;
    }

    public void PlacementSecondary()
    {
        if (PlacingType == SecurityOrderType.PatrolRoute && FinishPlacement(false))
            return;

        CancelPlacement();
    }

    private bool OnUse(in PointerInputCmdArgs args)
    {
        if (PlacingType == null)
            return TryOpenOfficerMenu(args.EntityUid);

        if (!_timing.IsFirstTimePredicted)
            return true;

        if (State is not { IsCommander: true })
        {
            CancelPlacement();
            return false;
        }

        if (!args.Coordinates.IsValid(EntityManager))
            return true;

        AddPoint(args.Coordinates);
        return true;
    }

    private bool TryOpenOfficerMenu(EntityUid target)
    {
        if (!_timing.IsFirstTimePredicted || _window is not { IsOpen: true } window ||
            State is not { IsCommander: true } state || !TryGetNetEntity(target, out var netEntity))
            return false;

        var officer = state.Officers.Find(officer => officer.Entity == netEntity);
        if (officer == null)
            return false;

        _inputSystem.CmdStates.SetState(EngineKeyFunctions.Use, BoundKeyState.Up);
        window.OpenOfficerMenu(officer);
        return true;
    }

    private bool OnUseSecondary(in PointerInputCmdArgs args)
    {
        if (PlacingType == null)
            return false;

        if (!_timing.IsFirstTimePredicted)
            return true;

        PlacementSecondary();
        return true;
    }

    public MapCoordinates GetMouseMapPosition()
    {
        return _eye.PixelToMap(_input.MouseScreenPosition);
    }

    public void RemoveOrder(int id)
    {
        RaiseNetworkEvent(new SecurityCommandRemoveOrderMessage(id));
        PlayUi(SecurityCommandSounds.OrderRemoved, -10f);
    }

    public void ClearOrders()
    {
        RaiseNetworkEvent(new SecurityCommandClearOrdersMessage());
        PlayUi(SecurityCommandSounds.OrdersCleared, -10f);
    }

    public void CreateSquad(string name, Robust.Shared.Maths.Color color)
    {
        RaiseNetworkEvent(new SecurityCommandCreateSquadMessage(name, color));
        PlayUi(SecurityCommandSounds.SquadCreated, -2f);
    }

    public void RemoveSquad(int id)
    {
        RaiseNetworkEvent(new SecurityCommandRemoveSquadMessage(id));
        PlayUi(SecurityCommandSounds.Unassigned, -10f);
    }

    public void AssignOfficer(NetEntity officer, int squadId)
    {
        RaiseNetworkEvent(new SecurityCommandAssignOfficerMessage(officer, squadId));
        PlayUi(SecurityCommandSounds.OfficerAssigned, -2f);
    }

    private void PlayUi(SoundSpecifier sound, float volume, float pitch = 1f)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        _audio.PlayGlobal(sound, Filter.Local(), false, AudioParams.Default.WithVolume(volume).WithPitchScale(pitch));
    }

    public bool IsRelevantToLocal(SecurityOrderData order)
    {
        if (State == null)
            return false;

        switch (order.TargetKind)
        {
            case SecurityOrderTargetKind.All:
                return true;
            case SecurityOrderTargetKind.Squad:
                return order.SquadId != 0 && order.SquadId == State.OwnSquadId;
            case SecurityOrderTargetKind.Officer:
                return _player.LocalEntity is { } local && order.Officer == GetNetEntity(local);
            default:
                return false;
        }
    }

    public SecuritySquadData? GetSquad(int id)
    {
        if (State == null || id == 0)
            return null;

        foreach (var squad in State.Squads)
        {
            if (squad.Id == id)
                return squad;
        }

        return null;
    }

    public string DescribeTarget(SecurityOrderData order)
    {
        switch (order.TargetKind)
        {
            case SecurityOrderTargetKind.Squad:
                var squad = GetSquad(order.SquadId);
                return squad != null
                    ? Loc.GetString("security-command-target-squad", ("name", squad.Name))
                    : Loc.GetString("security-command-target-unknown");
            case SecurityOrderTargetKind.Officer:
                if (State != null)
                {
                    foreach (var officer in State.Officers)
                    {
                        if (officer.Entity == order.Officer)
                            return Loc.GetString("security-command-target-officer", ("name", officer.Name));
                    }
                }

                return Loc.GetString("security-command-target-personal");
            default:
                return Loc.GetString("security-command-target-all");
        }
    }

    public static string GetTypeName(SecurityOrderType type)
    {
        return Robust.Shared.Localization.Loc.GetString(SecurityCommandConstants.GetTypeLoc(type));
    }

    public string? GetPostDutyText(SecurityOrderData order)
    {
        if (order.PostDurationMinutes <= 0)
            return null;

        if (!IsRelevantToLocal(order))
            return Loc.GetString("security-command-post-duration", ("minutes", order.PostDurationMinutes));

        return order.PostDutyStatus switch
        {
            SecurityPostDutyStatus.Serving => Loc.GetString("security-command-post-countdown",
                ("time", SecurityCommandConstants.FormatPostTime(order.PostDutyEndsAt - _timing.CurTime))),
            SecurityPostDutyStatus.Completed => Loc.GetString("security-command-post-completed"),
            SecurityPostDutyStatus.LeftEarly => Loc.GetString("security-command-post-left-early"),
            _ => Loc.GetString("security-command-post-waiting", ("minutes", order.PostDurationMinutes)),
        };
    }

    public static string GetDisplayText(SecurityOrderData order)
    {
        return string.IsNullOrEmpty(order.Text) ? GetTypeName(order.Type) : order.Text;
    }
}
