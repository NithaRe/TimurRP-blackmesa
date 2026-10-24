using System.Linq;
using System.Numerics;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Shared._BlackM.Lobby;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._BlackM.Lobby;


public sealed class LobbyRoundCameraSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly ViewSubscriberSystem _viewSubscriber = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly HashSet<ICommonSession> _viewers = new();
    private GameTicker _gameTicker = default!;
    private EntityUid? _camera;
    private EntityUid? _cameraTarget;
    private float _targetChangeTimer;
    private float _cameraFollowTimer;

    private const float TargetChangeInterval = 18f;
    private const float CameraFollowInterval = 0.1f;

    public override void Initialize()
    {
        base.Initialize();
        _gameTicker = EntityManager.System<GameTicker>();

        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<StationGridAddedEvent>(OnStationGridAdded);
        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
        ClearCamera();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_gameTicker.RunLevel != GameRunLevel.InRound || _camera is not { } camera || !Exists(camera))
            return;

        _targetChangeTimer += frameTime;
        _cameraFollowTimer += frameTime;

        if (_cameraTarget == null || !IsValidCameraTarget(_cameraTarget.Value) || _targetChangeTimer >= TargetChangeInterval)
        {
            SelectCameraTarget();
            _targetChangeTimer = 0f;
        }

        if (_cameraFollowTimer < CameraFollowInterval)
            return;

        _cameraFollowTimer = 0f;
        if (_cameraTarget is { } target && IsValidCameraTarget(target))
            FollowTarget(camera, target);
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New != GameRunLevel.InRound)
        {
            ClearCamera();
            return;
        }

        EnsureCamera();
        AddLobbyViewers();
    }

    private void AddLobbyViewers()
    {
        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status == SessionStatus.InGame && !_gameTicker.UserHasJoinedGame(session))
                AddViewer(session);
        }
    }

    private void OnStationGridAdded(StationGridAddedEvent ev)
    {
        if (_gameTicker.RunLevel != GameRunLevel.InRound || _camera != null)
            return;

        CreateCamera(ev.GridId);
        AddLobbyViewers();
    }

    private void OnPlayerJoinedLobby(PlayerJoinedLobbyEvent ev)
    {
        if (_gameTicker.RunLevel != GameRunLevel.InRound)
            return;

        EnsureCamera();
        AddViewer(ev.PlayerSession);
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        RemoveViewer(ev.Player);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs ev)
    {
        if (ev.NewStatus == SessionStatus.Disconnected)
            RemoveViewer(ev.Session, notify: false);
    }

    private void EnsureCamera()
    {
        if (_camera != null)
            return;

        var query = EntityQueryEnumerator<StationDataComponent>();
        while (query.MoveNext(out _, out var station))
        {
            foreach (var grid in station.Grids)
            {
                if (!Exists(grid))
                    continue;

                CreateCamera(grid);
                return;
            }
        }
    }

    private void CreateCamera(EntityUid grid)
    {
        if (_camera != null || !Exists(grid))
            return;

        _camera = SpawnAttachedTo("BlackMLobbyRoundCamera", new EntityCoordinates(grid, Vector2.Zero));
        AlignCameraToGrid(_camera.Value, grid);
    }

    private void SelectCameraTarget()
    {
        var targets = new List<EntityUid>();
        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status != SessionStatus.InGame || !_gameTicker.UserHasJoinedGame(session) ||
                session.AttachedEntity is not { } entity || !IsValidCameraTarget(entity))
                continue;

            targets.Add(entity);
        }

        if (targets.Count == 0)
        {
            _cameraTarget = null;
            return;
        }

        var next = targets[_random.Next(targets.Count)];
        if (targets.Count > 1 && next == _cameraTarget)
            next = targets[(_random.Next(targets.Count - 1) + targets.IndexOf(next) + 1) % targets.Count];

        _cameraTarget = next;
    }

    private bool IsValidCameraTarget(EntityUid target)
    {
        return Exists(target) && !HasComp<GhostComponent>(target) &&
               HasComp<TransformComponent>(target) && Transform(target).MapUid != null;
    }

    private void FollowTarget(EntityUid camera, EntityUid target)
    {
        var targetTransform = Transform(target);
        _transform.SetCoordinates(camera, targetTransform.Coordinates);

        if (targetTransform.GridUid is { } grid && Exists(grid))
            AlignCameraToGrid(camera, grid);
        else
            _transform.SetWorldRotation(camera, Angle.Zero);
    }

    private void AlignCameraToGrid(EntityUid camera, EntityUid grid)
    {
        _transform.SetWorldRotation(camera, _transform.GetWorldRotation(grid));
    }

    private void AddViewer(ICommonSession session)
    {
        if (_camera == null || session.Status != SessionStatus.InGame ||
            _gameTicker.UserHasJoinedGame(session) || !_viewers.Add(session))
            return;

        _viewSubscriber.AddViewSubscriber(_camera.Value, session);
        RaiseNetworkEvent(new LobbyRoundCameraEvent(GetNetEntity(_camera.Value)), session);
    }

    private void RemoveViewer(ICommonSession session, bool notify = true)
    {
        if (!_viewers.Remove(session))
            return;

        if (_camera != null && Exists(_camera.Value))
            _viewSubscriber.RemoveViewSubscriber(_camera.Value, session);

        if (notify && session.Status == SessionStatus.InGame)
            RaiseNetworkEvent(new LobbyRoundCameraEvent(null), session);
    }

    private void ClearCamera()
    {
        foreach (var viewer in _viewers.ToArray())
            RemoveViewer(viewer);

        if (_camera is { } camera && Exists(camera))
            QueueDel(camera);

        _camera = null;
        _cameraTarget = null;
        _targetChangeTimer = 0f;
        _cameraFollowTimer = 0f;
    }
}
