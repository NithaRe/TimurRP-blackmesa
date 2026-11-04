using System.Numerics;
using Content.Shared._BlackM.Gib;
using Content.Shared.Physics;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Client._BlackM.Gib;

public sealed class GibBurstSystem : EntitySystem, IGibWorld
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IMapManager _mapMan = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    private const float ScreenRange = 4.5f;

    private GibParticleOverlay _particles = default!;
    private GibScreenOverlay _screen = default!;
    private EntityQuery<OccluderComponent> _occluders;

    private readonly Dictionary<(MapId, Vector2i), bool> _visCache = new();
    private float _visTimer;

    public override void Initialize()
    {
        base.Initialize();
        _occluders = GetEntityQuery<OccluderComponent>();
        _particles = new GibParticleOverlay(_cache, this);
        _screen = new GibScreenOverlay(_cache);
        _overlays.AddOverlay(_particles);
        _overlays.AddOverlay(_particles.Floor);
        _overlays.AddOverlay(_screen);
        SubscribeNetworkEvent<GibBurstEvent>(OnBurst);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_particles);
        _overlays.RemoveOverlay(_particles.Floor);
        _overlays.RemoveOverlay(_screen);
    }

    public override void FrameUpdate(float frameTime)
    {
        _visTimer += frameTime;
        if (_visTimer >= 0.2f)
        {
            _visTimer = 0f;
            _visCache.Clear();
        }
    }

    private void OnBurst(GibBurstEvent ev)
    {
        if (!Visible(ev.Map, ev.Position))
            return;

        _particles.Burst(ev);

        if (!ev.Screen || _player.LocalEntity is not { } local || TerminatingOrDeleted(local))
            return;

        var pos = _xform.GetMapCoordinates(local);
        if (pos.MapId != ev.Map)
            return;

        var dist = (pos.Position - ev.Position).Length();
        if (dist >= ScreenRange)
            return;

        _screen.Flash(1f - dist / ScreenRange, ev.Color);
    }

    public void Drip(MapId map, Vector2 pos, Color color)
        => _particles.Drip(map, pos, color);

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
