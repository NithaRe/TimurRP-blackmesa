using System.Numerics;
using Content.Shared._BlackM.Monorail;
using Robust.Client.Graphics;
using Robust.Client.Placement;
using Robust.Client.Placement.Modes;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using static Robust.Client.Placement.PlacementManager;

namespace Content.Client._BlackM.Monorail;

public sealed class AlignMonorailTrack : SnapgridCenter
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;

    private readonly SharedTransformSystem _transformSystem;

    private const float ScanRadius = 14f;

    private const float MarkerDrawRadius = 10f;

    private const float MarkerRadius = 0.1f;

    private const float StepConnectRange = 0.05f;

    private static readonly Color FreeEndColor = Color.Yellow.WithAlpha(0.8f);
    private static readonly Color SnappedColor = Color.Cyan.WithAlpha(0.9f);
    private static readonly Color AxisColor = Color.White.WithAlpha(0.6f);

    private readonly Color _defaultValidColor;

    private string? _cachedProto;
    private MonorailTrackComponent? _cachedTrack;

    private readonly List<MonorailTrackEnd> _allEnds = new();
    private List<MonorailTrackEnd> _freeEnds = new();

    private bool _active;
    private bool _snapped;
    private MonorailSnapResult _snap;
    private MonorailTrackShape _shape;
    private EntityUid _frame;
    private Vector2 _cursor;

    public AlignMonorailTrack(PlacementManager pMan) : base(pMan)
    {
        IoCManager.InjectDependencies(this);
        _transformSystem = _entityManager.System<SharedTransformSystem>();
        _defaultValidColor = ValidPlaceColor;
    }

    public override void AlignPlacementMode(ScreenCoordinates mouseScreen)
    {
        _active = false;
        _snapped = false;
        ValidPlaceColor = _defaultValidColor;

        base.AlignPlacementMode(mouseScreen);

        if (!TryGetTrack(out var track))
            return;

        _shape = track.Shape;

        if (pManager.PlacementType != PlacementTypes.None)
        {
            GridDistancing = MonorailTrackGeometry.LineStep(_shape);
            return;
        }

        var raw = ScreenToCursorGrid(mouseScreen);
        if (!raw.EntityId.IsValid())
            return;

        _active = true;
        _frame = raw.EntityId;
        _cursor = raw.Position;

        var rot = (float) pManager.Direction.ToAngle().Theta;
        CollectEnds(raw.Position);

        var pos = MonorailTrackGeometry.SnapToStep(raw.Position, MonorailPlacementSettings.Step);

        if (MonorailPlacementSettings.Magnet)
        {
            if (MonorailPlacementSettings.Step > 0f &&
                MonorailTrackGeometry.TryFindSnap(_shape, pos, rot, _freeEnds, StepConnectRange,
                    MonorailTrackGeometry.MaxSnapAngle, out var onStep))
            {
                Accept(onStep);
                pos = onStep.Position;
            }
            else if (MonorailTrackGeometry.TryFindSnap(_shape, raw.Position, rot, _freeEnds, track.SnapRange,
                         MonorailTrackGeometry.MaxSnapAngle, out var magnet))
            {
                Accept(magnet);
                pos = magnet.Position;
            }
        }

        MouseCoords = new EntityCoordinates(_frame, pos);
        CurrentTile = GetTileRef(MouseCoords);
    }

    public override bool IsValidPosition(EntityCoordinates position)
    {
        if (pManager.CurrentPermission!.IsTile)
            return false;

        return RangeCheck(position);
    }

    public override void Render(in OverlayDrawArgs args)
    {
        base.Render(args);

        if (!_active || !_entityManager.EntityExists(_frame))
            return;

        var matrix = _transformSystem.GetWorldMatrix(_frame);
        var handle = args.WorldHandle;

        foreach (var end in _freeEnds)
        {
            if ((end.Pos - _cursor).LengthSquared() > MarkerDrawRadius * MarkerDrawRadius)
                continue;

            var from = Vector2.Transform(end.Pos, matrix);
            var to = Vector2.Transform(end.Pos + end.Dir * 0.35f, matrix);
            handle.DrawCircle(from, MarkerRadius, FreeEndColor);
            handle.DrawLine(from, to, FreeEndColor);
        }

        var rot = _snapped ? _snap.Rotation : (float) pManager.Direction.ToAngle().Theta;
        var pos = _snapped ? _snap.FinalPosition : MouseCoords.Position;
        var pts = MonorailTrackGeometry.ToFrame(_shape, pos, rot);
        var color = _snapped ? SnappedColor : AxisColor;
        for (var i = 0; i < pts.Count - 1; i++)
        {
            handle.DrawLine(Vector2.Transform(pts[i], matrix), Vector2.Transform(pts[i + 1], matrix), color);
        }

        if (_snapped)
            handle.DrawCircle(Vector2.Transform(_snap.Point, matrix), MarkerRadius * 1.8f, SnappedColor);
    }

    private void Accept(MonorailSnapResult snap)
    {
        _snapped = true;
        _snap = snap;
        ValidPlaceColor = SnappedColor;
    }

    private bool TryGetTrack(out MonorailTrackComponent track)
    {
        track = default!;
        var id = pManager.CurrentPermission?.EntityType;
        if (id == null)
            return false;

        if (id != _cachedProto)
        {
            _cachedProto = id;
            _cachedTrack = null;
            if (_protoManager.TryIndex<EntityPrototype>(id, out var proto) &&
                proto.TryGetComponent<MonorailTrackComponent>(out var comp, _entityManager.ComponentFactory))
            {
                _cachedTrack = comp;
            }
        }

        if (_cachedTrack == null)
            return false;

        track = _cachedTrack;
        return true;
    }

    private void CollectEnds(Vector2 around)
    {
        _allEnds.Clear();
        var query = _entityManager.EntityQueryEnumerator<MonorailTrackComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var track, out var xform))
        {
            if (xform.ParentUid != _frame)
                continue;

            if ((xform.LocalPosition - around).LengthSquared() > ScanRadius * ScanRadius)
                continue;

            MonorailTrackGeometry.AddOpenEnds(uid, track.Shape, MonorailTrackGeometry.ToFrame(track.Shape, xform), _allEnds);
        }

        _freeEnds = MonorailTrackGeometry.FreeEnds(_allEnds);
    }
}
