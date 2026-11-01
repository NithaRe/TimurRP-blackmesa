using System.Numerics;
using Content.Shared._BlackM.Shot;
using Content.Shared.Physics;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;

namespace Content.Client._BlackM.Shot;

public sealed class ShotSystem : EntitySystem, IShotWorld
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IMapManager _mapMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    private const string ImpactLight = "BlackMImpactLight";
    private const float TrailSpacing = 0.22f;

    private readonly Dictionary<EntityUid, ShotState> _states = new();
    private ShotOverlay _overlay = default!;
    private EntityQuery<OccluderComponent> _occluders;

    private readonly Dictionary<(MapId, Vector2i), bool> _visCache = new();
    private float _visTimer;

    public override void Initialize()
    {
        base.Initialize();
        _occluders = GetEntityQuery<OccluderComponent>();
        _overlay = new ShotOverlay(_cache, _states, this);
        _overlays.AddOverlay(_overlay);
        SubscribeLocalEvent<ShotComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_overlay);
        _states.Clear();
    }

    public override void FrameUpdate(float frameTime)
    {
        _visTimer += frameTime;
        if (_visTimer >= 0.2f)
        {
            _visTimer = 0f;
            _visCache.Clear();
        }

        var query = EntityQueryEnumerator<ShotComponent, TransformComponent, PhysicsComponent>();

        while (query.MoveNext(out var uid, out var shot, out var xform, out var body))
        {
            if (body.LinearVelocity.LengthSquared() < 9f)
                continue;

            var pos = _xform.GetWorldPosition(xform);
            var dir = _xform.GetWorldRotation(xform).ToWorldVec();
            var trail = shot.Smoke && shot.SmokeTrail;

            if (!_states.TryGetValue(uid, out var st))
            {
                st = new ShotState
                {
                    Comp = shot,
                    Map = xform.MapID,
                    Pos = pos,
                    Dir = dir,
                    Visible = shot.Tracer && _random.Prob(shot.TracerChance),
                    Phase = _random.NextFloat(6.28f),
                };
                _states[uid] = st;

                if (trail)
                {
                    _overlay.BeginTrail(uid, st.Map, shot);
                    _overlay.AddTrailPoint(uid, pos, dir, 0f);
                }
            }

            var step = (pos - st.Pos).Length();
            st.Dist += step;
            st.TrailAcc += step;
            st.Pos = pos;
            st.Dir = dir;
            st.Age += frameTime;

            if (trail)
            {
                st.TrailAcc = MathF.Min(st.TrailAcc, 4f);
                while (st.TrailAcc >= TrailSpacing)
                {
                    st.TrailAcc -= TrailSpacing;
                    _overlay.AddTrailPoint(uid, pos - dir * st.TrailAcc, dir, st.Dist - st.TrailAcc);
                }
            }
        }
    }

    private void OnShutdown(Entity<ShotComponent> ent, ref ComponentShutdown args)
    {
        _overlay.EndTrail(ent.Owner);

        if (!_states.Remove(ent.Owner, out var st))
            return;

        if (!ent.Comp.Impact || st.Dist < 0.6f || st.Age > 4f)
            return;

        if (!Visible(st.Map, st.Pos - st.Dir * 0.35f))
            return;

        _overlay.Impact(st.Map, st.Pos, st.Dir, ent.Comp);

        if (ent.Comp.Light)
            Spawn(ImpactLight, new MapCoordinates(st.Pos, st.Map));
    }

    public bool Visible(MapId map, Vector2 pos)
    {
        var eye = _eye.CurrentEye;
        if (!eye.DrawFov)
            return true;

        MapCoordinates origin;
        if (_player.LocalEntity is { } local && !TerminatingOrDeleted(local))
            origin = _xform.GetMapCoordinates(local);
        else
            origin = eye.Position;

        if (origin.MapId != map)
            return false;

        var key = (map, new Vector2i((int) MathF.Floor(pos.X), (int) MathF.Floor(pos.Y)));
        if (_visCache.TryGetValue(key, out var cached))
            return cached;

        var visible = !IsOccluded(origin.Position, pos, map);
        _visCache[key] = visible;
        return visible;
    }

    private bool IsOccluded(Vector2 from, Vector2 to, MapId map)
    {
        var delta = to - from;
        var len = delta.Length();

        if (len > 40f)
            return true;

        if (len < 0.6f)
            return false;

        var ray = new CollisionRay(from, delta / len, (int) CollisionGroup.Opaque);

        foreach (var hit in _physics.IntersectRay(map, ray, len, returnOnFirstHit: false))
        {
            if (hit.Distance >= len - 0.5f)
                continue;

            if (_occluders.TryComp(hit.HitEntity, out var occluder) && occluder.Enabled)
                return true;
        }

        return false;
    }

    public bool Blocked(MapId map, Vector2 pos)
    {
        var coords = new MapCoordinates(pos, map);
        if (!_mapMan.TryFindGridAt(coords, out var gridUid, out var grid))
            return false;

        var tile = _map.CoordinatesToTile(gridUid, grid, _map.MapToGrid(gridUid, coords));
        var enumerator = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);

        while (enumerator.MoveNext(out var anchored))
        {
            if (anchored != null && _occluders.HasComp(anchored.Value))
                return true;
        }

        return false;
    }
}
