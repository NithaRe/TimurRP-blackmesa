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
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._BlackM.Lobby;


public sealed class LobbyRoundCameraSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly ViewSubscriberSystem _viewSubscriber = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly HashSet<ICommonSession> _viewers = new();
    private GameTicker _gameTicker = default!;
    private EntityUid? _camera;

    private EntityUid? _cameraGrid;
    private Vector2 _pos;
    private Vector2 _vel;
    private Vector2 _waypoint;
    private bool _hasWaypoint;

    private const float CruiseSpeed = 1.3f;
    private const float SlowRadius = 6f;
    private const float ArriveRadius = 4f;
    private const float MinSpeedFactor = 0.35f;
    private const float Smoothing = 0.7f;
    private const float MinLeg = 12f;
    private const float MaxLeg = 50f;

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

        if (_gameTicker.RunLevel != GameRunLevel.InRound || _camera is not { } camera || !Exists(camera) ||
            _cameraGrid is not { } grid || !Exists(grid))
            return;

        var toTarget = _waypoint - _pos;
        var dist = toTarget.Length();

        if (!_hasWaypoint || dist < ArriveRadius)
        {
            PickWaypoint(grid);
            toTarget = _waypoint - _pos;
            dist = toTarget.Length();
        }

        var speed = CruiseSpeed * MathF.Max(MinSpeedFactor, MathF.Min(1f, dist / SlowRadius));
        var desired = dist > 0.001f ? toTarget / dist * speed : Vector2.Zero;

        _vel = Vector2.Lerp(_vel, desired, 1f - MathF.Exp(-Smoothing * frameTime));
        _pos += _vel * frameTime;

        _transform.SetCoordinates(camera, new EntityCoordinates(grid, _pos));
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
        StartDrift(grid);
    }

    private void StartDrift(EntityUid grid)
    {
        _cameraGrid = grid;
        _vel = Vector2.Zero;
        _hasWaypoint = false;

        if (TryRandomTile(grid, out var start))
            _pos = start;

        if (_camera is { } camera && Exists(camera))
            _transform.SetCoordinates(camera, new EntityCoordinates(grid, _pos));

        PickWaypoint(grid);
    }

    private void PickWaypoint(EntityUid grid)
    {
        if (!TryComp<MapGridComponent>(grid, out var gridComp))
            return;

        var tiles = _map.GetAllTiles(grid, gridComp).ToList();
        if (tiles.Count == 0)
            return;

        var best = _pos;
        var found = false;

        for (var i = 0; i < 20; i++)
        {
            var tile = tiles[_random.Next(tiles.Count)];
            var candidate = new Vector2(tile.GridIndices.X + 0.5f, tile.GridIndices.Y + 0.5f) * gridComp.TileSize;
            var leg = (candidate - _pos).Length();

            best = candidate;
            found = true;

            if (leg >= MinLeg && leg <= MaxLeg)
                break;
        }

        if (!found)
            return;

        _waypoint = best;
        _hasWaypoint = true;
    }

    private bool TryRandomTile(EntityUid grid, out Vector2 position)
    {
        position = default;

        if (!TryComp<MapGridComponent>(grid, out var gridComp))
            return false;

        var tiles = _map.GetAllTiles(grid, gridComp).ToList();
        if (tiles.Count == 0)
            return false;

        var tile = tiles[_random.Next(tiles.Count)];
        position = new Vector2(tile.GridIndices.X + 0.5f, tile.GridIndices.Y + 0.5f) * gridComp.TileSize;
        return true;
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
        _cameraGrid = null;
        _hasWaypoint = false;
        _vel = Vector2.Zero;
    }
}
