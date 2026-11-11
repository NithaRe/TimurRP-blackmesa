using System.Linq;
using System.Numerics;
using Content.Server.Administration.Managers;
using Content.Shared._BlackM.Monorail;
using Content.Shared.Administration;
using Content.Shared.Audio;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Placement;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using static Content.Shared._BlackM.Monorail.MonorailTrackGeometry;

namespace Content.Server._BlackM.Monorail;

public sealed partial class MonorailSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly FixtureSystem _fixtures = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedPointLightSystem _lights = default!;

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RiderScanInterval = TimeSpan.FromSeconds(0.2);

    private const float BoardInset = 0.12f;

    private const float LeaveOutset = 0.08f;

    private const float ManualSnapRange = 1.5f;

    private const float MobRadius = 0.35f;

    private const float SideSlack = 0.15f;

    private static readonly TimeSpan CooldownCleanupInterval = TimeSpan.FromSeconds(10);

    private static readonly VerbCategory MonorailVerbCategory = new("monorail-verb-category", null);

    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();
    private readonly HashSet<Entity<ItemComponent>> _items = new();
    private readonly List<EntityUid> _children = new();
    private readonly List<EntityUid> _pendingSnap = new();
    private readonly HashSet<Entity<MobStateComponent>> _hitMobs = new();
    private readonly Dictionary<EntityUid, TimeSpan> _hitCooldown = new();
    private TimeSpan _nextCooldownCleanup;

    private EntityQuery<MonorailTrackComponent> _trackQuery;
    private EntityQuery<MonorailSeatComponent> _seatQuery;
    private EntityQuery<MonorailStopComponent> _stopQuery;
    private EntityQuery<BuckleComponent> _buckleQuery;
    private EntityQuery<StrapComponent> _strapQuery;
    private EntityQuery<MobStateComponent> _mobQuery;
    private EntityQuery<ItemComponent> _itemQuery;

    public override void Initialize()
    {
        base.Initialize();

        _trackQuery = GetEntityQuery<MonorailTrackComponent>();
        _seatQuery = GetEntityQuery<MonorailSeatComponent>();
        _stopQuery = GetEntityQuery<MonorailStopComponent>();
        _buckleQuery = GetEntityQuery<BuckleComponent>();
        _strapQuery = GetEntityQuery<StrapComponent>();
        _mobQuery = GetEntityQuery<MobStateComponent>();
        _itemQuery = GetEntityQuery<ItemComponent>();

        SubscribeLocalEvent<MonorailCarComponent, MapInitEvent>(OnCarMapInit);
        SubscribeLocalEvent<MonorailCarComponent, ExaminedEvent>(OnCarExamined);
        SubscribeLocalEvent<MonorailCarComponent, EntityTerminatingEvent>(OnCarTerminating);

        SubscribeLocalEvent<MonorailTrackComponent, ComponentStartup>(OnTrackStartup);
        SubscribeLocalEvent<MonorailTrackComponent, ComponentShutdown>(OnTrackShutdown);
        SubscribeLocalEvent<MonorailTrackComponent, MoveEvent>(OnTrackMoved);
        SubscribeLocalEvent<MonorailTrackComponent, GetVerbsEvent<Verb>>(OnTrackVerbs);

        SubscribeLocalEvent<PlacementEntityEvent>(OnPlacement);
    }

    #region Track geometry

    private sealed class Piece
    {
        public EntityUid Uid;
        public List<Vector2> Points = default!;
        public bool OpenStart;
        public bool OpenEnd;
        public MonorailStopComponent? Stop;
        public readonly (int Piece, int End)[] Links = { (-1, 0), (-1, 0) };
    }

    #endregion

    #region Placement snapping

    private void OnPlacement(PlacementEntityEvent ev)
    {
        if (ev.PlacementEventAction != PlacementEventAction.Create)
            return;

        if (!_trackQuery.HasComp(ev.EditedEntity))
            return;

        _pendingSnap.Add(ev.EditedEntity);
    }

    public bool TrySnapTrack(EntityUid uid, MonorailTrackComponent? track = null, float? range = null)
    {
        if (!Resolve(uid, ref track, false))
            return false;

        var xform = Transform(uid);
        var frame = xform.ParentUid;
        if (!frame.IsValid())
            return false;

        var all = new List<MonorailTrackEnd>();
        var query = EntityQueryEnumerator<MonorailTrackComponent, TransformComponent>();
        while (query.MoveNext(out var other, out var otherTrack, out var otherXform))
        {
            if (other == uid || otherXform.ParentUid != frame)
                continue;

            MonorailTrackGeometry.AddOpenEnds(other, otherTrack.Shape,
                MonorailTrackGeometry.ToFrame(otherTrack.Shape, otherXform), all);
        }

        var free = MonorailTrackGeometry.FreeEnds(all);
        var rot = (float) xform.LocalRotation.Theta;
        if (!MonorailTrackGeometry.TryFindSnap(track.Shape, xform.LocalPosition, rot, free,
                range ?? track.SnapFixRange, MonorailTrackGeometry.MaxSnapAngle, out var snap))
        {
            return false;
        }

        if (snap.Delta == 0f && MonorailTrackGeometry.IsAlmostEqual(snap.FinalPosition, xform.LocalPosition))
            return true;

        if (snap.Delta != 0f)
            _xform.SetLocalRotation(uid, new Angle(snap.Rotation), xform);

        _xform.SetLocalPosition(uid, snap.FinalPosition, xform);
        return true;
    }

    public void AlignTrackToGrid(EntityUid uid, TransformComponent? xform = null)
    {
        if (!Resolve(uid, ref xform))
            return;

        var rot = MonorailTrackGeometry.SnapToCardinal((float) xform.LocalRotation.Theta);
        var pos = MonorailTrackGeometry.SnapToStep(xform.LocalPosition, 1f);
        _xform.SetLocalRotation(uid, new Angle(rot), xform);
        _xform.SetLocalPosition(uid, pos, xform);
    }

    public int AlignAllTracksToGrid(EntityUid frame)
    {
        var list = new List<EntityUid>();
        var query = EntityQueryEnumerator<MonorailTrackComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.ParentUid == frame)
                list.Add(uid);
        }

        foreach (var uid in list)
        {
            AlignTrackToGrid(uid);
        }

        return list.Count;
    }

    private void OnTrackVerbs(EntityUid uid, MonorailTrackComponent component, GetVerbsEvent<Verb> args)
    {
        if (!TryComp(args.User, out ActorComponent? actor) ||
            !_admin.HasAdminFlag(actor.PlayerSession, AdminFlags.Mapping))
        {
            return;
        }

        var user = args.User;

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("monorail-verb-align-grid"),
            Category = MonorailVerbCategory,
            Priority = 3,
            Act = () =>
            {
                AlignTrackToGrid(uid);
                _popup.PopupEntity(Loc.GetString("monorail-verb-align-grid-done"), uid, user);
            },
        });

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("monorail-verb-snap"),
            Category = MonorailVerbCategory,
            Priority = 2,
            Act = () =>
            {
                var ok = TrySnapTrack(uid, range: ManualSnapRange);
                _popup.PopupEntity(Loc.GetString(ok ? "monorail-verb-snap-done" : "monorail-verb-snap-fail"), uid, user);
            },
        });

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("monorail-verb-align-all"),
            Category = MonorailVerbCategory,
            Priority = 1,
            ConfirmationPopup = true,
            Act = () =>
            {
                var frame = Transform(uid).ParentUid;
                if (!frame.IsValid())
                    return;

                var count = AlignAllTracksToGrid(frame);
                _popup.PopupEntity(Loc.GetString("monorail-verb-align-all-done", ("count", count)), uid, user);
            },
        });
    }

    private void OnTrackStartup(Entity<MonorailTrackComponent> ent, ref ComponentStartup args)
    {
        MarkAllRoutesDirty();
    }

    private void OnTrackShutdown(Entity<MonorailTrackComponent> ent, ref ComponentShutdown args)
    {
        MarkAllRoutesDirty();
    }

    private void OnTrackMoved(Entity<MonorailTrackComponent> ent, ref MoveEvent args)
    {
        MarkAllRoutesDirty();
    }

    private void MarkAllRoutesDirty()
    {
        var query = EntityQueryEnumerator<MonorailCarComponent>();
        while (query.MoveNext(out _, out var car))
        {
            car.RouteDirty = true;
        }
    }

    #endregion

    #region Route building

    private bool TryBuildRoute(EntityUid uid, MonorailCarComponent car, TransformComponent xform)
    {
        var carWorld = _xform.GetWorldPosition(xform);
        var invCache = new Dictionary<EntityUid, Matrix3x2>();
        var bestDist = car.SearchRange;
        EntityUid? startTrack = null;
        var frame = EntityUid.Invalid;

        var query = EntityQueryEnumerator<MonorailTrackComponent, TransformComponent>();
        while (query.MoveNext(out var tUid, out var track, out var tXform))
        {
            if (tXform.MapID != xform.MapID || !tXform.ParentUid.IsValid())
                continue;

            if (!invCache.TryGetValue(tXform.ParentUid, out var inv))
            {
                inv = _xform.GetInvWorldMatrix(Transform(tXform.ParentUid));
                invCache[tXform.ParentUid] = inv;
            }

            var local = Vector2.Transform(carWorld, inv);
            var d = DistanceToPolyline(local, ToFrame(track.Shape, tXform), out _);
            if (d >= bestDist)
                continue;

            bestDist = d;
            startTrack = tUid;
            frame = tXform.ParentUid;
        }

        if (startTrack == null)
        {
            car.LastError = "no track nearby";
            return false;
        }

        var pieces = new List<Piece>();
        var startIndex = -1;
        query = EntityQueryEnumerator<MonorailTrackComponent, TransformComponent>();
        while (query.MoveNext(out var tUid, out var track, out var tXform))
        {
            if (tXform.ParentUid != frame)
                continue;

            if (tUid == startTrack)
                startIndex = pieces.Count;

            _stopQuery.TryComp(tUid, out var stop);
            pieces.Add(new Piece
            {
                Uid = tUid,
                Points = ToFrame(track.Shape, tXform),
                OpenStart = true,
                OpenEnd = track.Shape != MonorailTrackShape.End,
                Stop = stop,
            });
        }

        var tol2 = car.ConnectTolerance * car.ConnectTolerance;
        for (var i = 0; i < pieces.Count; i++)
        {
            for (var e = 0; e < 2; e++)
            {
                if (!IsOpen(pieces[i], e))
                    continue;

                var p = EndPoint(pieces[i], e);
                var best = tol2;
                for (var j = 0; j < pieces.Count; j++)
                {
                    if (j == i)
                        continue;

                    for (var f = 0; f < 2; f++)
                    {
                        if (!IsOpen(pieces[j], f))
                            continue;

                        var d = (EndPoint(pieces[j], f) - p).LengthSquared();
                        if (d >= best)
                            continue;

                        best = d;
                        pieces[i].Links[e] = (j, f);
                    }
                }
            }
        }

        var chain = new List<(int Piece, bool Reversed)> { (startIndex, false) };
        var visited = new HashSet<int> { startIndex };
        var loop = false;

        var cur = startIndex;
        var exit = 1;
        while (true)
        {
            var (j, entry) = pieces[cur].Links[exit];
            if (j < 0)
                break;

            if (j == startIndex)
            {
                loop = entry == 0;
                break;
            }

            if (!visited.Add(j))
                break;

            chain.Add((j, entry == 1));
            exit = 1 - entry;
            cur = j;
        }

        if (!loop)
        {
            cur = startIndex;
            exit = 0;
            while (true)
            {
                var (j, entry) = pieces[cur].Links[exit];
                if (j < 0 || !visited.Add(j))
                    break;

                chain.Insert(0, (j, entry == 0));
                exit = 1 - entry;
                cur = j;
            }
        }

        var points = new List<Vector2>();
        var cumulative = new List<float>();
        var stops = new List<(float S, string? Name, EntityUid Uid)>();
        var length = 0f;

        foreach (var (index, reversed) in chain)
        {
            var piece = pieces[index];
            var pts = reversed ? Enumerable.Reverse(piece.Points).ToList() : piece.Points;
            var pieceStart = length;

            for (var k = 0; k < pts.Count; k++)
            {
                if (points.Count > 0 && k == 0)
                    continue;

                if (points.Count > 0)
                    length += (pts[k] - points[^1]).Length();

                points.Add(pts[k]);
                cumulative.Add(length);
            }

            if (piece.Stop is { } stop && stop.Line == car.Line)
                stops.Add(((pieceStart + length) / 2f, stop.StopName, piece.Uid));
        }

        if (loop && points.Count > 1)
        {
            length += (points[0] - points[^1]).Length();
            points.Add(points[0]);
            cumulative.Add(length);
        }

        if (points.Count < 2 || length < 0.5f)
        {
            car.LastError = "track too short";
            return false;
        }

        if (stops.Count < (loop ? 1 : 2))
        {
            car.LastError = loop ? "no stations on loop" : "need at least 2 stations on the line";
            return false;
        }

        stops.Sort((a, b) => a.S.CompareTo(b.S));

        if (xform.ParentUid != frame)
            _xform.SetParent(uid, frame);

        car.Frame = frame;
        car.Points = points;
        car.Cumulative = cumulative;
        car.Length = length;
        car.Loop = loop;
        car.Stops = stops.Select(s => s.S).ToList();
        car.StopNames = stops.Select(s => s.Name).ToList();
        car.StopEntities = stops.Select(s => s.Uid).ToList();
        car.RouteDirty = false;
        car.LastError = null;
        return true;
    }

    private static bool IsOpen(Piece piece, int end)
    {
        return end == 0 ? piece.OpenStart : piece.OpenEnd;
    }

    private static Vector2 EndPoint(Piece piece, int end)
    {
        return end == 0 ? piece.Points[0] : piece.Points[^1];
    }

    private static float Project(MonorailCarComponent car, Vector2 local)
    {
        DistanceToPolyline(local, car.Points, out var along);
        return along;
    }

    private static Vector2 PointAt(MonorailCarComponent car, float s)
    {
        var pts = car.Points;
        var cum = car.Cumulative;

        if (car.Loop)
        {
            s %= car.Length;
            if (s < 0)
                s += car.Length;
        }
        else if (s <= 0f)
        {
            return pts[0] + Vector2.Normalize(pts[1] - pts[0]) * s;
        }
        else if (s >= car.Length)
        {
            return pts[^1] + Vector2.Normalize(pts[^1] - pts[^2]) * (s - car.Length);
        }

        var lo = 0;
        var hi = cum.Count - 1;
        while (hi - lo > 1)
        {
            var mid = (lo + hi) / 2;
            if (cum[mid] <= s)
                lo = mid;
            else
                hi = mid;
        }

        var seg = cum[hi] - cum[lo];
        var t = seg > 1e-5f ? (s - cum[lo]) / seg : 0f;
        return Vector2.Lerp(pts[lo], pts[hi], t);
    }

    private static (Vector2 Pos, float Rot) PoseAt(MonorailCarComponent car, float s)
    {
        var half = car.BogieSpacing / 2f;
        var rear = PointAt(car, s - half);
        var front = PointAt(car, s + half);
        var dir = front - rear;

        if (dir.LengthSquared() < 1e-6f)
            dir = PointAt(car, s + 0.05f) - PointAt(car, s - 0.05f);

        var rot = MathF.Atan2(dir.Y, dir.X) + MathF.PI / 2f;
        return ((rear + front) / 2f, rot);
    }

    private void ApplyPose(EntityUid uid, MonorailCarComponent car, TransformComponent xform)
    {
        var (pos, rot) = PoseAt(car, car.Position);
        car.FramePosition = pos;
        car.FrameRotation = rot;

        if (!car.Frame.IsValid() || TerminatingOrDeleted(car.Frame))
        {
            car.RouteDirty = true;
            return;
        }

        _xform.SetCoordinates(uid, xform, new EntityCoordinates(car.Frame, pos), new Angle(rot));
    }

    private Vector2 ToFrameLocal(EntityUid frame, TransformComponent xform)
    {
        if (xform.ParentUid == frame)
            return xform.LocalPosition;

        var world = _xform.GetWorldPosition(xform);
        return Vector2.Transform(world, _xform.GetInvWorldMatrix(Transform(frame)));
    }

    #endregion

    #region Car lifecycle

    private void OnCarMapInit(Entity<MonorailCarComponent> ent, ref MapInitEvent args)
    {
        foreach (var seat in ent.Comp.Seats)
        {
            var seatUid = Spawn(ent.Comp.SeatPrototype, new EntityCoordinates(ent, seat.Offset));
            _xform.SetLocalRotation(seatUid, Angle.FromDegrees(seat.Rotation));
        }

        SpawnHeadlights(ent, ent.Comp);
        SetDoors(ent, ent.Comp, true, false);
    }

    private void SpawnHeadlights(EntityUid uid, MonorailCarComponent car)
    {
        if (car.HeadlightPrototype is not { } proto)
            return;

        var y = car.BodyHalfExtents.Y - car.HeadlightInset;
        car.HeadlightNorth = SpawnHeadlight(uid, proto, new Vector2(0f, y), 180f + car.HeadlightAngleOffset);
        car.HeadlightSouth = SpawnHeadlight(uid, proto, new Vector2(0f, -y), car.HeadlightAngleOffset);

        SetHeadlight(car.HeadlightNorth, car.HeadlightColor, car.HeadlightEnergy);
        SetHeadlight(car.HeadlightSouth, car.HeadlightColor, car.HeadlightEnergy);
    }

    private EntityUid SpawnHeadlight(EntityUid car, EntProtoId proto, Vector2 offset, float degrees)
    {
        var lamp = Spawn(proto, new EntityCoordinates(car, offset));
        _xform.SetLocalRotation(lamp, Angle.FromDegrees(degrees));
        return lamp;
    }

    private void UpdateHeadlights(MonorailCarComponent car, float travelSign)
    {
        if (travelSign == 0f)
            return;

        var noseSouth = travelSign > 0f;
        var front = noseSouth ? car.HeadlightSouth : car.HeadlightNorth;
        var rear = noseSouth ? car.HeadlightNorth : car.HeadlightSouth;

        SetHeadlight(front, car.HeadlightColor, car.HeadlightEnergy);
        SetHeadlight(rear, car.TailLightColor, car.TailLightEnergy);
    }

    private void SetHeadlight(EntityUid? lamp, Color color, float energy)
    {
        if (lamp is not { } uid || TerminatingOrDeleted(uid))
            return;

        _lights.SetColor(uid, color);
        _lights.SetEnergy(uid, energy);
    }

    private void OnCarTerminating(Entity<MonorailCarComponent> ent, ref EntityTerminatingEvent args)
    {
        CollectChildren(Transform(ent));
        var children = _children.ToArray();

        foreach (var child in children)
        {
            if (TerminatingOrDeleted(child))
                continue;

            if (_strapQuery.TryComp(child, out var strap))
            {
                foreach (var buckled in strap.BuckledEntities.ToArray())
                {
                    _buckle.Unbuckle((buckled, null), null);
                    if (!TerminatingOrDeleted(buckled))
                        _xform.AttachToGridOrMap(buckled);
                }

                continue;
            }

            if (_mobQuery.HasComp(child) || _itemQuery.HasComp(child))
                _xform.AttachToGridOrMap(child);
        }
    }

    private void CollectChildren(TransformComponent xform)
    {
        _children.Clear();
        var enumerator = xform.ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            _children.Add(child);
        }
    }

    private void SetDoors(EntityUid uid, MonorailCarComponent car, bool open, bool playSound = true)
    {
        if (!open && !car.DoorsClosed)
            ScanBoarding(uid, car, Transform(uid));

        car.DoorsClosed = !open;
        car.Moving = !open;
        Dirty(uid, car);
        _appearance.SetData(uid, MonorailCarVisuals.DoorsOpen, open);
        UpdateDoorFixtures(uid, car);

        if (!open)
            ClearDoorways(uid, car);

        if (playSound)
            _audio.PlayPvs(open ? car.DoorsOpenSound : car.DoorsCloseSound, uid);
    }

    #endregion

    #region Update

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pendingSnap.Count > 0)
        {
            foreach (var track in _pendingSnap)
            {
                if (!TerminatingOrDeleted(track))
                    TrySnapTrack(track);
            }

            _pendingSnap.Clear();
        }

        var now = _timing.CurTime;
        if (_hitCooldown.Count > 0 && now >= _nextCooldownCleanup)
        {
            _nextCooldownCleanup = now + CooldownCleanupInterval;
            foreach (var (hitEnt, until) in _hitCooldown.ToArray())
            {
                if (now >= until || TerminatingOrDeleted(hitEnt))
                    _hitCooldown.Remove(hitEnt);
            }
        }

        var query = EntityQueryEnumerator<MonorailCarComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var car, out var xform))
        {
            if (Paused(uid))
                continue;

            var prevPosition = car.Position;
            var wasMoving = car.State == MonorailCarState.Moving;

            switch (car.State)
            {
                case MonorailCarState.Uninitialized:
                    if (now >= car.NextRetry && !TryStart(uid, car, xform, now))
                        car.NextRetry = now + RetryDelay;
                    break;
                case MonorailCarState.Dwelling:
                    UpdateDwelling(uid, car, xform, now);
                    break;
                case MonorailCarState.Moving:
                    UpdateMoving(uid, car, xform, now);
                    break;
            }

            car.Speed = wasMoving && car.State == MonorailCarState.Moving && frameTime > 0f
                ? (car.Position - prevPosition) / frameTime
                : 0f;

            if (car.State == MonorailCarState.Moving)
                UpdateCollisions(uid, car, xform, now);

            if (car.State != MonorailCarState.Uninitialized)
                UpdateRiders(uid, car, xform, now);
        }
    }

    private bool TryStart(EntityUid uid, MonorailCarComponent car, TransformComponent xform, TimeSpan now)
    {
        if (!TryBuildRoute(uid, car, xform))
            return false;

        car.Position = Project(car, ToFrameLocal(car.Frame, xform));

        var nearest = NearestStop(car, car.Position);
        ApplyPose(uid, car, xform);

        if (MathF.Abs(StopDelta(car, car.Position, car.Stops[nearest])) < 0.05f)
        {
            car.Position = car.Stops[nearest];
            ApplyPose(uid, car, xform);
            Arrive(uid, car, nearest, now, false);
            return true;
        }

        car.Direction = car.Loop || car.Stops[nearest] > car.Position ? 1 : -1;
        StartTrip(uid, car, nearest, now, false);
        return true;
    }

    private static int NearestStop(MonorailCarComponent car, float s)
    {
        var best = 0;
        var bestDist = float.MaxValue;
        for (var i = 0; i < car.Stops.Count; i++)
        {
            var d = MathF.Abs(StopDelta(car, s, car.Stops[i]));
            if (d >= bestDist)
                continue;

            bestDist = d;
            best = i;
        }

        return best;
    }

    private static float StopDelta(MonorailCarComponent car, float from, float to)
    {
        var d = to - from;
        if (!car.Loop)
            return d;

        d %= car.Length;
        if (d > car.Length / 2f)
            d -= car.Length;
        if (d < -car.Length / 2f)
            d += car.Length;
        return d;
    }

    private void UpdateDwelling(EntityUid uid, MonorailCarComponent car, TransformComponent xform, TimeSpan now)
    {
        if (car.RouteDirty)
        {
            if (!TryBuildRoute(uid, car, xform))
            {
                car.State = MonorailCarState.Uninitialized;
                car.NextRetry = now + RetryDelay;
                if (car.DoorsClosed)
                    SetDoors(uid, car, true);
                return;
            }

            car.Position = Project(car, ToFrameLocal(car.Frame, xform));
            car.StopIndex = NearestStop(car, car.Position);
            car.Position = car.Stops[car.StopIndex];
            ApplyPose(uid, car, xform);
        }

        var left = (float) (car.NextDeparture - now).TotalSeconds;

        if (!car.WarningGiven && left <= car.DepartureWarning)
        {
            car.WarningGiven = true;
            _popup.PopupEntity(Loc.GetString("monorail-car-departure-warning",
                    ("seconds", (int) MathF.Ceiling(MathF.Max(left, 0f)))),
                uid,
                PopupType.Medium);
            _audio.PlayPvs(car.DepartureWarningSound, uid);
        }

        if (!car.DoorsClosed && left <= car.DoorCloseTime)
        {
            SetDoors(uid, car, false);
            _popup.PopupEntity(Loc.GetString("monorail-car-doors-closing"), uid, PopupType.Small);
        }

        if (left > 0f)
            return;

        var next = NextStopIndex(car);
        StartTrip(uid, car, next, now, true);
    }

    private int NextStopIndex(MonorailCarComponent car)
    {
        var count = car.Stops.Count;
        if (car.Loop)
            return (car.StopIndex + 1) % count;

        var next = car.StopIndex + car.Direction;
        if (next < 0 || next >= count)
        {
            car.Direction = -car.Direction;
            next = car.StopIndex + car.Direction;
        }

        return Math.Clamp(next, 0, count - 1);
    }

    private void StartTrip(EntityUid uid, MonorailCarComponent car, int target, TimeSpan now, bool playSound)
    {
        var to = car.Stops[target];
        if (car.Loop)
        {
            var forward = (to - car.Position) % car.Length;
            if (forward < 0.05f)
                forward += car.Length;
            to = car.Position + forward;
        }

        if (!car.DoorsClosed)
            SetDoors(uid, car, false, playSound);

        car.StopIndex = target;
        car.TripFrom = car.Position;
        car.TripTo = to;
        car.TripStart = now;
        car.TripDuration = TripDuration(MathF.Abs(to - car.Position), car.MaxSpeed, car.Acceleration);
        car.State = MonorailCarState.Moving;

        UpdateHeadlights(car, MathF.Sign(to - car.Position));
        _ambient.SetAmbience(uid, true);
        if (playSound)
            _audio.PlayPvs(car.DepartSound, uid);
    }

    private void UpdateMoving(EntityUid uid, MonorailCarComponent car, TransformComponent xform, TimeSpan now)
    {
        var t = (float) (now - car.TripStart).TotalSeconds;
        var dist = MathF.Abs(car.TripTo - car.TripFrom);
        var sign = MathF.Sign(car.TripTo - car.TripFrom);

        if (t >= car.TripDuration)
        {
            car.Position = car.TripTo;
            if (car.Loop)
            {
                car.Position %= car.Length;
                if (car.Position < 0)
                    car.Position += car.Length;
            }

            ApplyPose(uid, car, xform);
            Arrive(uid, car, car.StopIndex, now, true);
            return;
        }

        car.Position = car.TripFrom + sign * TravelledAt(t, dist, car.MaxSpeed, car.Acceleration);
        ApplyPose(uid, car, xform);
    }

    private void Arrive(EntityUid uid, MonorailCarComponent car, int stop, TimeSpan now, bool announce)
    {
        car.State = MonorailCarState.Dwelling;
        car.StopIndex = stop;
        car.NextDeparture = now + TimeSpan.FromSeconds(car.DwellTime);
        car.WarningGiven = false;

        _ambient.SetAmbience(uid, false);
        SetDoors(uid, car, true, announce);

        if (!announce)
            return;

        _audio.PlayPvs(car.ArriveSound, uid);
        var name = car.StopNames[stop];
        _popup.PopupEntity(string.IsNullOrWhiteSpace(name)
                ? Loc.GetString("monorail-car-arrived")
                : Loc.GetString("monorail-car-arrived-named", ("name", name)),
            uid,
            PopupType.Medium);
    }

    private static float TripDuration(float dist, float vmax, float acc)
    {
        var tAcc = vmax / acc;
        var dAcc = 0.5f * acc * tAcc * tAcc;
        if (2f * dAcc >= dist)
            return 2f * MathF.Sqrt(dist / acc);

        return 2f * tAcc + (dist - 2f * dAcc) / vmax;
    }

    private static float TravelledAt(float t, float dist, float vmax, float acc)
    {
        var tAcc = vmax / acc;
        var dAcc = 0.5f * acc * tAcc * tAcc;

        if (2f * dAcc >= dist)
        {
            var tHalf = MathF.Sqrt(dist / acc);
            if (t <= tHalf)
                return 0.5f * acc * t * t;

            var td = MathF.Min(t - tHalf, tHalf);
            var vPeak = acc * tHalf;
            return dist / 2f + vPeak * td - 0.5f * acc * td * td;
        }

        var tCruise = (dist - 2f * dAcc) / vmax;
        if (t <= tAcc)
            return 0.5f * acc * t * t;
        if (t <= tAcc + tCruise)
            return dAcc + vmax * (t - tAcc);

        var tb = MathF.Min(t - tAcc - tCruise, tAcc);
        return dAcc + vmax * tCruise + vmax * tb - 0.5f * acc * tb * tb;
    }

    #endregion

    #region Standing passengers

    private static bool InsideFloor(Vector2 local, Vector2 ext, float inset)
    {
        return MathF.Abs(local.X) <= ext.X - inset && MathF.Abs(local.Y) <= ext.Y - inset;
    }

    private void UpdateRiders(EntityUid uid, MonorailCarComponent car, TransformComponent xform, TimeSpan now)
    {
        var ext = car.FloorHalfExtents;
        var locked = car.DoorsClosed && car.LockStandingWhileMoving;

        CollectChildren(xform);
        foreach (var child in _children)
        {
            if (_seatQuery.HasComp(child))
                continue;

            var isMob = _mobQuery.HasComp(child);
            if (!isMob && !_itemQuery.HasComp(child))
                continue;

            if (_buckleQuery.TryComp(child, out var buckle) && buckle.Buckled)
                continue;

            var cx = Transform(child);
            var p = cx.LocalPosition;
            if (InsideFloor(p, ext, -LeaveOutset))
                continue;

            if (locked && isMob)
            {
                var clamped = new Vector2(
                    Math.Clamp(p.X, -ext.X + 0.02f, ext.X - 0.02f),
                    Math.Clamp(p.Y, -ext.Y + 0.02f, ext.Y - 0.02f));
                _xform.SetLocalPosition(child, clamped, cx);
                continue;
            }

            if (car.Frame.IsValid() && !TerminatingOrDeleted(car.Frame))
                _xform.SetParent(child, car.Frame);
            else
                _xform.AttachToGridOrMap(child, cx);
        }

        if (car.DoorsClosed || now < car.NextRiderScan)
            return;

        car.NextRiderScan = now + RiderScanInterval;
        ScanBoarding(uid, car, xform);
    }

    private void ScanBoarding(EntityUid uid, MonorailCarComponent car, TransformComponent xform)
    {
        if (!car.Frame.IsValid() || TerminatingOrDeleted(car.Frame))
            return;

        var range = car.FloorHalfExtents.Length() + 0.5f;

        _mobs.Clear();
        _lookup.GetEntitiesInRange(xform.Coordinates, range, _mobs, flags: LookupFlags.Uncontained);
        foreach (var mob in _mobs)
        {
            TryBoard(uid, car, xform, mob.Owner);
        }

        if (!car.CarryItems)
            return;

        _items.Clear();
        _lookup.GetEntitiesInRange(xform.Coordinates, range, _items, flags: LookupFlags.Uncontained);
        foreach (var item in _items)
        {
            TryBoard(uid, car, xform, item.Owner);
        }
    }

    private void TryBoard(EntityUid uid, MonorailCarComponent car, TransformComponent carXform, EntityUid ent)
    {
        if (ent == uid)
            return;

        var ex = Transform(ent);
        if (ex.Anchored)
            return;

        if (ex.ParentUid != car.Frame && ex.ParentUid != ex.MapUid)
            return;

        if (_buckleQuery.TryComp(ent, out var buckle) && buckle.Buckled)
            return;

        var rel = ToFrameLocal(car.Frame, ex) - car.FramePosition;
        var local = Rotate(rel, -car.FrameRotation);
        if (!InsideFloor(local, car.FloorHalfExtents, BoardInset))
            return;

        _xform.SetParent(ent, uid);
    }

    #endregion

    #region Collision (v3.2)

    private void UpdateDoorFixtures(EntityUid uid, MonorailCarComponent car)
    {
        foreach (var id in car.DoorFixtures)
        {
            var fixture = _fixtures.GetFixtureOrNull(uid, id);
            if (fixture == null)
                continue;

            _physics.SetHard(uid, fixture, car.DoorsClosed);
        }
    }

    private Vector2 CarLocal(MonorailCarComponent car, TransformComponent ex)
    {
        var rel = ToFrameLocal(car.Frame, ex) - car.FramePosition;
        return Rotate(rel, -car.FrameRotation);
    }

    private void MoveToCarLocal(MonorailCarComponent car, EntityUid ent, TransformComponent ex, Vector2 local)
    {
        var framePos = car.FramePosition + Rotate(local, car.FrameRotation);
        if (ex.ParentUid == car.Frame)
        {
            _xform.SetLocalPosition(ent, framePos, ex);
            return;
        }

        _xform.SetWorldPosition(ent, Vector2.Transform(framePos, _xform.GetWorldMatrix(car.Frame)));
    }

    private Vector2 CarDirToWorld(MonorailCarComponent car, Vector2 localDir)
    {
        var frameDir = Rotate(localDir, car.FrameRotation);
        return _xform.GetWorldRotation(car.Frame).RotateVec(frameDir);
    }

    private bool IsRiding(EntityUid ent, EntityUid car)
    {
        var parent = Transform(ent).ParentUid;
        for (var i = 0; i < 4 && parent.IsValid(); i++)
        {
            if (parent == car)
                return true;

            parent = Transform(parent).ParentUid;
        }

        return false;
    }

    private void ClearDoorways(EntityUid uid, MonorailCarComponent car)
    {
        var body = car.BodyHalfExtents;
        var innerX = body.X - car.WallThickness;
        var insideX = MathF.Max(innerX - MobRadius - 0.02f, 0f);
        var reachY = car.DoorHalfWidth + MobRadius;
        var carXform = Transform(uid);

        CollectChildren(carXform);
        foreach (var child in _children)
        {
            if (!_mobQuery.HasComp(child))
                continue;

            if (_buckleQuery.TryComp(child, out var childBuckle) && childBuckle.Buckled)
                continue;

            var cx = Transform(child);
            var p = cx.LocalPosition;
            if (MathF.Abs(p.Y) >= reachY || MathF.Abs(p.X) <= insideX)
                continue;

            _xform.SetLocalPosition(child, new Vector2(MathF.Sign(p.X) * insideX, p.Y), cx);
        }

        if (!car.Frame.IsValid() || TerminatingOrDeleted(car.Frame))
            return;

        _hitMobs.Clear();
        _lookup.GetEntitiesInRange(carXform.Coordinates, body.Length() + 1f, _hitMobs, flags: LookupFlags.Uncontained);
        foreach (var mob in _hitMobs)
        {
            var ent = mob.Owner;
            if (ent == uid || IsRiding(ent, uid))
                continue;

            var ex = Transform(ent);
            if (ex.Anchored)
                continue;

            if (_buckleQuery.TryComp(ent, out var buckle) && buckle.Buckled)
                continue;

            var local = CarLocal(car, ex);
            var ax = MathF.Abs(local.X);
            if (MathF.Abs(local.Y) >= reachY || ax <= insideX || ax >= body.X + MobRadius)
                continue;

            var side = local.X >= 0f ? 1f : -1f;
            if (ax <= innerX)
            {
                MoveToCarLocal(car, ent, ex, new Vector2(side * insideX, local.Y));
                TryBoard(uid, car, carXform, ent);
            }
            else
            {
                MoveToCarLocal(car, ent, ex, new Vector2(side * (body.X + MobRadius + 0.05f), local.Y));
            }
        }
    }

    private void UpdateCollisions(EntityUid uid, MonorailCarComponent car, TransformComponent xform, TimeSpan now)
    {
        if (!car.HitEnabled || !car.Frame.IsValid() || TerminatingOrDeleted(car.Frame))
            return;

        var speed = MathF.Abs(car.Speed);
        if (speed < 0.05f)
            return;

        var nose = car.Speed > 0f ? -1f : 1f;
        var body = car.BodyHalfExtents;
        var ahead = car.HitLookahead + speed * car.HitLookaheadTime;
        var horn = car.HornSound != null ? car.HornRange : 0f;
        var range = body.Length() + MathF.Max(ahead, horn) + 1f;

        _hitMobs.Clear();
        _lookup.GetEntitiesInRange(xform.Coordinates, range, _hitMobs, flags: LookupFlags.Uncontained);

        var honk = false;
        foreach (var mob in _hitMobs)
        {
            var ent = mob.Owner;
            if (ent == uid || IsRiding(ent, uid))
                continue;

            var ex = Transform(ent);
            if (ex.Anchored || ex.MapID != xform.MapID)
                continue;

            if (_buckleQuery.TryComp(ent, out var buckle) && buckle.Buckled)
                continue;

            var local = CarLocal(car, ex);
            if (MathF.Abs(local.X) > body.X + MobRadius - SideSlack)
                continue;

            var along = local.Y * nose;
            if (along < -body.Y - MobRadius + SideSlack)
                continue;

            if (along > body.Y + MobRadius + ahead)
            {
                if (along <= body.Y + horn)
                    honk = true;

                continue;
            }

            if (_hitCooldown.TryGetValue(ent, out var until) && now < until)
                continue;

            _hitCooldown[ent] = now + TimeSpan.FromSeconds(car.HitCooldown);
            HitMob(uid, car, ent, ex, local, nose, speed);
        }

        if (honk && now >= car.NextHorn)
        {
            car.NextHorn = now + TimeSpan.FromSeconds(car.HornCooldown);
            _audio.PlayPvs(car.HornSound, uid);
        }
    }

    private void HitMob(EntityUid uid, MonorailCarComponent car, EntityUid victim, TransformComponent ex,
        Vector2 local, float nose, float speed)
    {
        var body = car.BodyHalfExtents;
        var side = local.X >= 0f ? 1f : -1f;
        var power = Math.Clamp(speed / MathF.Max(car.MaxSpeed, 0.01f), 0f, 1f);
        var clearX = body.X + MobRadius + 0.05f;
        var clearY = body.Y + MobRadius + 0.1f;
        var along = local.Y * nose;

        if (MathF.Abs(local.X) < clearX && MathF.Abs(local.Y) < clearY)
        {
            var toSide = clearX - MathF.Abs(local.X);
            var toFront = clearY - along;
            local = toSide <= toFront
                ? new Vector2(side * clearX, local.Y)
                : new Vector2(local.X, nose * clearY);
            MoveToCarLocal(car, victim, ex, local);
        }

        var hard = speed >= car.HitMinSpeed;
        if (hard)
        {
            if (car.HitDamage != null)
                _damageable.TryChangeDamage(victim, car.HitDamage * power, origin: uid, canMiss: false);

            _stun.TryKnockdown(victim, TimeSpan.FromSeconds(car.HitKnockdownTime * (0.5f + 0.5f * power)), true);
        }

        var lateral = CarDirToWorld(car, new Vector2(side, 0f));
        var forward = CarDirToWorld(car, new Vector2(0f, nose));
        var latDist = MathF.Max(0.6f, clearX - MathF.Abs(local.X) + 0.6f);
        var fwdDist = hard ? 0.5f + 1.5f * power : 0.3f;
        var throwSpeed = hard ? MathF.Max(5f, car.HitThrowSpeed * power) : 4f;
        _throwing.TryThrow(victim, lateral * latDist + forward * fwdDist, throwSpeed,
            recoil: false, playSound: false, doSpin: false);

        var msg = Loc.GetString(hard ? "monorail-car-hit" : "monorail-car-shove",
            ("target", Identity.Entity(victim, EntityManager)));
        _popup.PopupEntity(msg, victim, hard ? PopupType.MediumCaution : PopupType.Small);

        if (hard)
            _audio.PlayPvs(car.HitSound, victim);
    }

    #endregion

    #region Examine

    private void OnCarExamined(Entity<MonorailCarComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var car = ent.Comp;
        switch (car.State)
        {
            case MonorailCarState.Dwelling:
                var left = Math.Max(0, (int) Math.Ceiling((car.NextDeparture - _timing.CurTime).TotalSeconds));
                args.PushMarkup(Loc.GetString("monorail-car-examine-departs", ("seconds", left)));
                break;
            case MonorailCarState.Moving:
                args.PushMarkup(Loc.GetString("monorail-car-examine-moving"));
                break;
            default:
                args.PushMarkup(Loc.GetString("monorail-car-examine-no-route"));
                return;
        }

        var next = car.State == MonorailCarState.Moving ? car.StopIndex : PeekNextStop(car);
        if (next >= 0 && next < car.StopNames.Count && !string.IsNullOrWhiteSpace(car.StopNames[next]))
            args.PushMarkup(Loc.GetString("monorail-car-examine-next", ("name", car.StopNames[next]!)));

        var riders = 0;
        CollectChildren(Transform(ent));
        foreach (var child in _children)
        {
            if (_mobQuery.HasComp(child))
                riders++;
            else if (_strapQuery.TryComp(child, out var strap))
                riders += strap.BuckledEntities.Count;
        }

        args.PushMarkup(Loc.GetString("monorail-car-examine-passengers",
            ("count", riders),
            ("seats", car.Seats.Count)));
    }

    private static int PeekNextStop(MonorailCarComponent car)
    {
        var count = car.Stops.Count;
        if (count == 0)
            return -1;
        if (car.Loop)
            return (car.StopIndex + 1) % count;

        var next = car.StopIndex + car.Direction;
        if (next < 0 || next >= count)
            next = car.StopIndex - car.Direction;
        return Math.Clamp(next, 0, count - 1);
    }

    #endregion
}
