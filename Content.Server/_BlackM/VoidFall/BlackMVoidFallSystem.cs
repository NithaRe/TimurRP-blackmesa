using System.Numerics;
using Content.Shared._BlackM.CCVar;
using Content.Shared._BlackM.VoidFall;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.IdentityManagement;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Parallax;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.VoidFall;

// Drops mobs void maps.
public sealed class BlackMVoidFallSystem : SharedBlackMVoidFallSystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(0.2);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(0.35);
    private static readonly TimeSpan KnockdownTime = TimeSpan.FromSeconds(2.5);
    private const float EdgeTolerance = 0.2f;
    private const float SafeSearchRadius = 12f;
    private const int StartPopups = 6;
    private const int LandPopups = 3;

    private static readonly ProtoId<TagPrototype> CatwalkTag = "Catwalk";
    private static readonly ProtoId<DamageTypePrototype> BluntDamage = "Blunt";
    private static readonly SoundSpecifier FallSound = new SoundPathSpecifier("/Audio/Effects/falling.ogg");
    private static readonly SoundSpecifier ScreamSound = new SoundPathSpecifier("/Audio/Voice/Human/wilhelm_scream.ogg");
    private static readonly SoundSpecifier LandSound = new SoundPathSpecifier("/Audio/Effects/bodyfall1.ogg");

    private static readonly Vector2[] Probes =
    {
        Vector2.Zero,
        new(EdgeTolerance, 0f),
        new(-EdgeTolerance, 0f),
        new(0f, EdgeTolerance),
        new(0f, -EdgeTolerance),
    };

    private readonly Dictionary<EntityUid, TimeSpan> _overVoid = new();
    private readonly Dictionary<EntityUid, EntityCoordinates> _lastSafe = new();
    private readonly HashSet<string> _parallaxes = new();
    private readonly List<EntityUid> _buffer = new();
    private List<Entity<MapGridComponent>> _grids = new();

    private TimeSpan _nextCheck;
    private TimeSpan _nextCleanup;
    private bool _enabled;
    private bool _lethal;
    private float _damage;
    private float _screamChance;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => Reset());

        Subs.CVar(_cfg, BlackMCVars.VoidFallEnabled, v => _enabled = v, true);
        Subs.CVar(_cfg, BlackMCVars.VoidFallLethal, v => _lethal = v, true);
        Subs.CVar(_cfg, BlackMCVars.VoidFallDamage, v => _damage = v, true);
        Subs.CVar(_cfg, BlackMCVars.VoidFallScreamChance, v => _screamChance = v, true);
        Subs.CVar(_cfg, BlackMCVars.VoidFallParallaxes, OnParallaxesChanged, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        _buffer.Clear();
        var falling = EntityQueryEnumerator<BlackMVoidFallingComponent>();
        while (falling.MoveNext(out var uid, out var comp))
        {
            if (comp.EndTime <= now)
                _buffer.Add(uid);
        }

        foreach (var uid in _buffer)
        {
            Land(uid);
        }

        if (now >= _nextCleanup)
        {
            _nextCleanup = now + CleanupInterval;
            Cleanup();
        }

        if (!_enabled || now < _nextCheck)
            return;

        _nextCheck = now + CheckInterval;

        _buffer.Clear();
        var query = EntityQueryEnumerator<MobStateComponent, TransformComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out _, out var xform, out var body))
        {
            if (!IsVoidMap(xform.MapUid) || !CanFall(uid, xform, body) || IsSupported(uid, xform))
            {
                _overVoid.Remove(uid);
                continue;
            }

            if (!_overVoid.TryGetValue(uid, out var since))
            {
                _overVoid[uid] = now;
                continue;
            }

            if (now - since >= Grace)
                _buffer.Add(uid);
        }

        foreach (var uid in _buffer)
        {
            StartFalling(uid);
        }
    }

    public bool IsVoidMap(EntityUid? map)
    {
        if (map is not { } uid)
            return false;

        if (TryComp<BlackMVoidMapComponent>(uid, out var marker))
            return marker.Enabled;

        return TryComp<ParallaxComponent>(uid, out var parallax) && _parallaxes.Contains(parallax.Parallax);
    }

    private bool CanFall(EntityUid uid, TransformComponent xform, PhysicsComponent body)
    {
        if (xform.Anchored || Paused(uid) || HasComp<BlackMVoidFallingComponent>(uid))
            return false;

        if (body.BodyStatus == BodyStatus.InAir || _container.IsEntityOrParentInContainer(uid))
            return false;

        if (HasComp<GhostComponent>(uid) ||
            HasComp<ThrownItemComponent>(uid) ||
            HasComp<JetpackUserComponent>(uid) ||
            HasComp<CanMoveInAirComponent>(uid) ||
            HasComp<MovementIgnoreGravityComponent>(uid))
        {
            return false;
        }

        return !TryComp<BuckleComponent>(uid, out var buckle) || !buckle.Buckled;
    }

    private bool IsSupported(EntityUid uid, TransformComponent xform)
    {
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        for (var i = 0; i < Probes.Length; i++)
        {
            var tile = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates.Offset(Probes[i]));
            if (!HasFloor(gridUid, grid, tile))
                continue;

            if (i == 0)
                _lastSafe[uid] = _map.GridTileToLocal(gridUid, grid, tile);

            return true;
        }

        return false;
    }

    private bool HasFloor(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        if (_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) && !tileRef.Tile.IsEmpty)
            return true;

        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var ent))
        {
            if (ent is { } support && _tag.HasTag(support, CatwalkTag))
                return true;
        }

        return false;
    }

    private void StartFalling(EntityUid uid)
    {
        _overVoid.Remove(uid);

        var comp = EnsureComp<BlackMVoidFallingComponent>(uid);
        comp.EndTime = _timing.CurTime + comp.Duration;
        Dirty(uid, comp);

        _physics.SetLinearVelocity(uid, Vector2.Zero);
        _audio.PlayPvs(FallSound, uid);

        if (_random.Prob(Math.Clamp(_screamChance, 0f, 1f)))
            _audio.PlayPvs(ScreamSound, uid);

        var message = Loc.GetString($"blackm-void-fall-start-{_random.Next(1, StartPopups + 1)}",
            ("name", Identity.Entity(uid, EntityManager)));
        _popup.PopupEntity(message, uid, PopupType.MediumCaution);
    }

    private void Land(EntityUid uid)
    {
        RemComp<BlackMVoidFallingComponent>(uid);

        if (TerminatingOrDeleted(uid))
            return;

        if (_lethal || !TryGetSafeSpot(uid, out var safe))
        {
            _popup.PopupCoordinates(Loc.GetString("blackm-void-fall-gone", ("name", Identity.Entity(uid, EntityManager))),
                Transform(uid).Coordinates, PopupType.LargeCaution);
            Forget(uid);
            QueueDel(uid);
            return;
        }

        _transform.SetCoordinates(uid, safe);
        _transform.AttachToGridOrMap(uid);
        _physics.SetLinearVelocity(uid, Vector2.Zero);

        if (_damage > 0f)
            _damageable.TryChangeDamage(uid, new DamageSpecifier(_proto.Index(BluntDamage), _damage));

        _stun.TryKnockdown(uid, KnockdownTime, refresh: true, drop: false);
        _audio.PlayPvs(LandSound, uid);

        var message = Loc.GetString($"blackm-void-fall-land-{_random.Next(1, LandPopups + 1)}",
            ("name", Identity.Entity(uid, EntityManager)));
        _popup.PopupEntity(message, uid, PopupType.Medium);
    }

    private bool TryGetSafeSpot(EntityUid uid, out EntityCoordinates coords)
    {
        if (_lastSafe.Remove(uid, out coords) &&
            !TerminatingOrDeleted(coords.EntityId) &&
            TryComp<MapGridComponent>(coords.EntityId, out var lastGrid) &&
            HasFloor(coords.EntityId, lastGrid, _map.CoordinatesToTile(coords.EntityId, lastGrid, coords)))
        {
            return true;
        }

        var mapPos = _transform.GetMapCoordinates(uid);
        var box = Box2.CenteredAround(mapPos.Position, new Vector2(SafeSearchRadius * 2f));
        var best = float.MaxValue;
        var found = false;
        coords = default;

        _grids.Clear();
        _mapManager.FindGridsIntersecting(mapPos.MapId, box, ref _grids);

        foreach (var grid in _grids)
        {
            foreach (var tileRef in _map.GetTilesIntersecting(grid.Owner, grid.Comp, box))
            {
                if (tileRef.Tile.IsEmpty || _turf.IsTileBlocked(tileRef, CollisionGroup.Impassable))
                    continue;

                var local = _map.GridTileToLocal(grid.Owner, grid.Comp, tileRef.GridIndices);
                var distance = (_transform.ToMapCoordinates(local).Position - mapPos.Position).LengthSquared();
                if (distance >= best)
                    continue;

                best = distance;
                coords = local;
                found = true;
            }
        }

        return found;
    }

    private void OnParallaxesChanged(string value)
    {
        _parallaxes.Clear();
        foreach (var id in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            _parallaxes.Add(id);
        }
    }

    private void Forget(EntityUid uid)
    {
        _overVoid.Remove(uid);
        _lastSafe.Remove(uid);
    }

    private void Cleanup()
    {
        _buffer.Clear();
        foreach (var uid in _lastSafe.Keys)
        {
            if (TerminatingOrDeleted(uid))
                _buffer.Add(uid);
        }

        foreach (var uid in _overVoid.Keys)
        {
            if (TerminatingOrDeleted(uid))
                _buffer.Add(uid);
        }

        foreach (var uid in _buffer)
        {
            Forget(uid);
        }

        _buffer.Clear();
    }

    private void Reset()
    {
        _overVoid.Clear();
        _lastSafe.Clear();
    }
}
