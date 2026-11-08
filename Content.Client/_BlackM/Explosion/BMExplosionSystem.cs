using System.Numerics;
using Content.Client.Explosion;
using Content.Shared.Explosion;
using Content.Shared.Explosion.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.Explosion;

/// <summary>
/// Client-only replacement for the vanilla explosion visuals.
/// </summary>
public sealed class BMExplosionSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPointLightSystem _lights = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private const string ScorchProto = "BMExplosionScorch";
    private const string LightProto = "ExplosionLight";
    private const float Spread = 0.035f;
    private const int MaxParticles = 2500;
    private const int MaxScorchPerExplosion = 60;
    private const int MaxScorches = 300;
    private const float ScorchLife = 60f;
    private const float ScorchFade = 20f;
    private const float EffectTimeout = 30f;

    private static readonly Color DefaultHot = new(1f, 0.96f, 0.78f, 1f);
    private static readonly Color DefaultMid = new(1f, 0.59f, 0.16f, 1f);
    private static readonly Color DefaultLate = new(0.7f, 0.2f, 0.06f, 1f);
    private static readonly Color DefaultSmoke = new(0.16f, 0.14f, 0.13f, 1f);

    public readonly List<BMExplosionEffect> Effects = new();

    private readonly Dictionary<NetEntity, float> _seen = new();
    private readonly List<NetEntity> _stale = new();
    private readonly List<(EntityUid Uid, float Born)> _scorches = new();
    private BMExplosionOverlay _overlay = default!;
    private double _epoch;
    private float _nextPrune;
    private int _particles;

    public float Now => (float) (_timing.RealTime.TotalSeconds - _epoch);

    public override void Initialize()
    {
        base.Initialize();
        _epoch = _timing.RealTime.TotalSeconds;
        _overlay = new BMExplosionOverlay(this, _cache, _proto);
        _overlayMan.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayMan.RemoveOverlay(_overlay);
        Effects.Clear();
        _scorches.Clear();
        _seen.Clear();
        _particles = 0;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        // Vanilla ExplosionOverlaySystem owns ComponentInit/Remove/HandleState, so only its overlay is removed.
        if (_overlayMan.HasOverlay<ExplosionOverlay>())
            _overlayMan.RemoveOverlay<ExplosionOverlay>();

        var now = Now;
        var query = EntityQueryEnumerator<ExplosionVisualsComponent>();
        while (query.MoveNext(out var uid, out var visuals))
        {
            if (visuals.Intensity.Count == 0)
                continue;

            var net = GetNetEntity(uid);
            var isNew = !_seen.ContainsKey(net);
            _seen[net] = now;

            if (isNew)
                Begin(uid, visuals, now);
        }

        if (now >= _nextPrune)
        {
            _nextPrune = now + 2f;
            foreach (var (net, seen) in _seen)
            {
                if (now - seen > 10f)
                    _stale.Add(net);
            }

            foreach (var net in _stale)
                _seen.Remove(net);

            _stale.Clear();
        }

        for (var i = Effects.Count - 1; i >= 0; i--)
        {
            var fx = Effects[i];
            if (UpdateEffect(fx, now, frameTime))
                continue;

            DeleteLight(fx);
            _particles -= fx.Particles.Count;
            Effects.RemoveAt(i);
        }

        UpdateScorches(now);
    }

    private void Begin(EntityUid uid, ExplosionVisualsComponent visuals, float now)
    {
        if (TryComp<ExplosionVisualsTexturesComponent>(uid, out var textures) &&
            textures.LightEntity.IsValid() &&
            !TerminatingOrDeleted(textures.LightEntity))
        {
            var vanilla = _lights.EnsureLight(textures.LightEntity);
            _lights.SetEnabled(textures.LightEntity, false, vanilla);
        }

        var mapId = visuals.Epicenter.MapId;
        if (mapId == MapId.Nullspace || !_map.MapExists(mapId))
            return;

        var iterations = visuals.Intensity.Count;
        var fx = new BMExplosionEffect
        {
            Source = uid,
            Map = mapId,
            Epicenter = visuals.Epicenter.Position,
            Start = now,
            Iterations = iterations,
            Scale = Math.Clamp(iterations / 6f, 0.4f, 3f),
            LightSeed = _random.NextFloat(100f),
            ByIteration = new List<BMBurnTile>[iterations],
        };

        for (var j = 0; j < iterations; j++)
        {
            fx.ByIteration[j] = new List<BMBurnTile>();
        }

        ApplyColors(fx, visuals.ExplosionType);

        var total = 0;
        foreach (var sets in visuals.Tiles.Values)
        {
            total += Count(sets);
        }

        if (visuals.SpaceTiles != null)
            total += Count(visuals.SpaceTiles);

        fx.Lod = total > 1500;
        var smokeChance = Math.Clamp(600f / Math.Max(total, 1), 0.08f, 0.55f);
        var scorches = 0;

        foreach (var (grid, sets) in visuals.Tiles)
        {
            if (!TryComp<MapGridComponent>(grid, out var gridComp))
                continue;

            var group = new BMBurnGroup
            {
                Grid = grid,
                TileSize = gridComp.TileSize,
                Matrix = _transform.GetWorldMatrix(grid),
                LocalEpicenter = Vector2.Transform(fx.Epicenter, _transform.GetInvWorldMatrix(grid)),
            };

            AddTiles(fx, group, sets, visuals, smokeChance, ref scorches, true);
        }

        if (visuals.SpaceTiles != null)
        {
            Matrix3x2.Invert(visuals.SpaceMatrix, out var invSpace);
            var group = new BMBurnGroup
            {
                Grid = EntityUid.Invalid,
                TileSize = visuals.SpaceTileSize,
                Matrix = visuals.SpaceMatrix,
                LocalEpicenter = Vector2.Transform(fx.Epicenter, invSpace),
            };

            AddTiles(fx, group, visuals.SpaceTiles, visuals, smokeChance, ref scorches, false);
        }

        if (fx.Tiles.Count == 0)
            return;

        fx.LightPeak = Math.Clamp(2.5f + 0.35f * iterations, 3f, 9f);
        fx.Light = Spawn(LightProto, visuals.Epicenter);
        var light = _lights.EnsureLight(fx.Light);
        _lights.SetRadius(fx.Light, Math.Clamp(4f + 1.4f * iterations, 5f, 24f), light);
        _lights.SetEnergy(fx.Light, 0f, light);
        _lights.SetColor(fx.Light, fx.Hot, light);

        var speed = MathF.Sqrt(fx.Scale);

        var sparks = Math.Min(12 + 4 * iterations, 120);
        for (var i = 0; i < sparks; i++)
        {
            var dir = RandomDir();
            AddParticle(new BMParticle
            {
                Kind = BMParticleKind.Spark,
                Pos = fx.Epicenter + dir * _random.NextFloat(0f, 0.4f),
                Vel = dir * _random.NextFloat(3f, 9f) * speed,
                Drag = 2.5f,
                Life = _random.NextFloat(0.35f, 0.9f),
            }, fx);
        }

        var debris = Math.Min(6 + 2 * iterations, 50);
        for (var i = 0; i < debris; i++)
        {
            var dir = RandomDir();
            AddParticle(new BMParticle
            {
                Kind = BMParticleKind.Debris,
                Pos = fx.Epicenter + dir * _random.NextFloat(0f, 0.3f),
                Vel = dir * _random.NextFloat(2f, 6f) * speed,
                Drag = 1.5f,
                Life = _random.NextFloat(0.6f, 1.2f),
                Size = _random.NextFloat(0.12f, 0.22f),
                Rot = _random.NextFloat(MathF.Tau),
                Spin = _random.NextFloat(-12f, 12f),
                Variant = _random.Next(3),
            }, fx);
        }

        var puffs = _random.Next(3, 7);
        for (var i = 0; i < puffs; i++)
        {
            var dir = RandomDir();
            AddParticle(new BMParticle
            {
                Kind = BMParticleKind.Smoke,
                Pos = fx.Epicenter + dir * _random.NextFloat(0f, iterations * 0.25f),
                Vel = dir * _random.NextFloat(0.1f, 0.35f),
                Drag = 0.4f,
                Age = -_random.NextFloat(0.15f, 0.5f),
                Life = _random.NextFloat(5f, 7f),
                Size = fx.Scale,
                EndSize = (iterations * 0.6f + 2f) * _random.NextFloat(0.8f, 1.1f),
                Alpha = 0.4f,
                Rot = _random.NextFloat(MathF.Tau),
                Spin = _random.NextFloat(-0.3f, 0.3f),
                Variant = _random.Next(4),
            }, fx);
        }

        Effects.Add(fx);
    }

    private void AddTiles(
        BMExplosionEffect fx,
        BMBurnGroup group,
        Dictionary<int, List<Vector2i>> sets,
        ExplosionVisualsComponent visuals,
        float smokeChance,
        ref int scorches,
        bool onGrid)
    {
        var g = fx.Groups.Count;
        fx.Groups.Add(group);

        foreach (var (j, tiles) in sets)
        {
            if (j < 0 || j >= fx.Iterations)
                continue;

            var intensity = visuals.Intensity[j];
            var heat = Math.Clamp(intensity / 20f, 0f, 1f);
            var scorchChance = Math.Clamp(intensity / 10f, 0.15f, 0.7f);

            foreach (var index in tiles)
            {
                var life = (0.5f + 0.4f * heat) * _random.NextFloat(0.85f, 1.15f);
                var tile = new BMBurnTile
                {
                    Group = g,
                    Index = index,
                    Local = (index + Vector2Helpers.Half) * group.TileSize,
                    Iteration = j,
                    Heat = heat,
                    Life = life,
                    Jitter = _random.NextFloat(0f, 0.04f),
                    Seed = _random.NextFloat(MathF.Tau),
                    Spin = _random.NextFloat(-1.5f, 1.5f),
                    Variant = _random.Next(4),
                };

                if (_random.Prob(smokeChance))
                    tile.SmokeAt = Math.Max(0.1f, life * 0.55f + _random.NextFloat(-0.15f, 0.3f));

                if (onGrid && intensity >= 3f && scorches < MaxScorchPerExplosion && _random.Prob(scorchChance))
                {
                    tile.ScorchAt = 0.35f;
                    scorches++;
                }

                fx.Tiles.Add(tile);
                fx.ByIteration[j].Add(tile);
            }
        }
    }

    private bool UpdateEffect(BMExplosionEffect fx, float now, float dt)
    {
        var age = now - fx.Start;
        if (age > EffectTimeout || !_map.MapExists(fx.Map))
            return false;

        // Finished or deleted explosions ignite whatever is left.
        var progress = int.MaxValue;
        if (!TerminatingOrDeleted(fx.Source))
        {
            if (_appearance.TryGetData(fx.Source, ExplosionAppearanceData.Progress, out int p))
                progress = p;
            else if (age < 2f)
                progress = -1;
        }

        var limit = Math.Min(progress, fx.Iterations - 1);
        while (fx.Reached <= limit)
        {
            var at = MathF.Max(now, fx.Start + fx.Reached * Spread);
            foreach (var tile in fx.ByIteration[fx.Reached])
            {
                tile.Ignite = at + tile.Jitter;
            }

            fx.Reached++;
        }

        var pending = fx.Reached < fx.Iterations;

        foreach (var group in fx.Groups)
        {
            if (!group.Grid.IsValid())
                continue;

            group.Valid = !TerminatingOrDeleted(group.Grid) && HasComp<MapGridComponent>(group.Grid);
            if (group.Valid)
                group.Matrix = _transform.GetWorldMatrix(group.Grid);
        }

        var burning = 0;
        foreach (var tile in fx.Tiles)
        {
            if (tile.Ignite > now)
            {
                pending = true;
                continue;
            }

            var t = now - tile.Ignite;
            var group = fx.Groups[tile.Group];

            if (!tile.Lit)
            {
                tile.Lit = true;
                if (group.Valid && _random.Prob(0.15f))
                    SpawnEmber(fx, tile);
            }

            if (t < tile.Life)
                burning++;

            if (tile.SmokeAt >= 0f)
            {
                if (t >= tile.SmokeAt)
                {
                    if (group.Valid)
                        SpawnSmoke(fx, tile, group);

                    tile.SmokeAt = -1f;
                }
                else
                {
                    pending = true;
                }
            }

            if (tile.ScorchAt >= 0f)
            {
                if (t >= tile.ScorchAt)
                {
                    if (group.Valid)
                        SpawnScorch(tile, group, now);

                    tile.ScorchAt = -1f;
                }
                else
                {
                    pending = true;
                }
            }
        }

        UpdateLight(fx, age, burning);

        for (var i = fx.Particles.Count - 1; i >= 0; i--)
        {
            var p = fx.Particles[i];
            if (p.Age < 0f)
            {
                p.Age += dt;
                continue;
            }

            p.Age += dt;
            if (p.Age >= p.Life)
            {
                fx.Particles[i] = fx.Particles[^1];
                fx.Particles.RemoveAt(fx.Particles.Count - 1);
                _particles--;
                continue;
            }

            p.Vel *= MathF.Exp(-p.Drag * dt);
            p.Pos += p.Vel * dt;
            p.Rot += p.Spin * dt;
        }

        return pending || burning > 0 || fx.Particles.Count > 0 || fx.Light.IsValid();
    }

    private void UpdateLight(BMExplosionEffect fx, float age, int burning)
    {
        if (!fx.Light.IsValid())
            return;

        if (TerminatingOrDeleted(fx.Light))
        {
            fx.Light = EntityUid.Invalid;
            return;
        }

        var ramp = Math.Clamp(age / 0.06f, 0f, 1f);
        var frac = burning / (float) fx.Tiles.Count;
        var flicker = 0.9f + 0.1f * MathF.Sin(age * 31f + fx.LightSeed) * MathF.Sin(age * 17f + fx.LightSeed * 0.7f);
        var energy = ramp * (fx.LightPeak * MathF.Exp(-3.5f * age) + 1.5f * frac) * flicker;

        if (energy < 0.05f && age > 0.5f && burning == 0)
        {
            DeleteLight(fx);
            return;
        }

        var light = _lights.EnsureLight(fx.Light);
        _lights.SetEnergy(fx.Light, energy, light);
        _lights.SetColor(fx.Light, Color.InterpolateBetween(fx.Hot, fx.LightColor, Math.Clamp(age / 0.3f, 0f, 1f)), light);
    }

    private void SpawnEmber(BMExplosionEffect fx, BMBurnTile tile)
    {
        AddParticle(new BMParticle
        {
            Kind = BMParticleKind.Ember,
            Group = tile.Group,
            Pos = tile.Local + RandomDir() * _random.NextFloat(0f, 0.4f),
            Vel = RandomDir() * _random.NextFloat(0.5f, 1.5f),
            Drag = 1.2f,
            Life = _random.NextFloat(0.6f, 1.4f),
            Size = _random.NextFloat(0.08f, 0.16f),
            Seed = _random.NextFloat(100f),
        }, fx);
    }

    private void SpawnSmoke(BMExplosionEffect fx, BMBurnTile tile, BMBurnGroup group)
    {
        var outward = tile.Local - group.LocalEpicenter;
        var dist = outward.Length();
        var dir = dist > 0.01f ? outward / dist : RandomDir();
        var falloff = 1f - Math.Clamp(dist / Math.Max(fx.Iterations, 1) * 0.5f, 0f, 0.5f);

        AddParticle(new BMParticle
        {
            Kind = BMParticleKind.Smoke,
            Group = tile.Group,
            Pos = tile.Local + RandomDir() * _random.NextFloat(0f, 0.3f),
            Vel = dir * _random.NextFloat(0.25f, 0.7f) * falloff + RandomDir() * _random.NextFloat(0f, 0.15f),
            Drag = 0.6f,
            Life = _random.NextFloat(2.5f, 4.5f) * (0.85f + 0.15f * fx.Scale),
            Size = 0.9f * group.TileSize,
            EndSize = 2.2f * group.TileSize * _random.NextFloat(0.85f, 1.2f),
            Alpha = 0.55f * (0.7f + 0.3f * tile.Heat),
            Rot = _random.NextFloat(MathF.Tau),
            Spin = _random.NextFloat(-0.4f, 0.4f),
            Variant = _random.Next(4),
        }, fx);
    }

    private void SpawnScorch(BMBurnTile tile, BMBurnGroup group, float now)
    {
        if (!TryComp<MapGridComponent>(group.Grid, out var grid) ||
            _map.GetTileRef(group.Grid, grid, tile.Index).Tile.IsEmpty)
        {
            return;
        }

        var offset = new Vector2(_random.NextFloat(-0.25f, 0.25f), _random.NextFloat(-0.25f, 0.25f));
        var uid = Spawn(ScorchProto, new EntityCoordinates(group.Grid, tile.Local + offset));
        _transform.SetLocalRotation(uid, new Angle(_random.NextFloat(MathF.Tau)));

        if (TryComp<SpriteComponent>(uid, out var sprite))
        {
            _sprite.SetScale((uid, sprite), new Vector2(_random.NextFloat(0.6f, 1f)));
            _sprite.LayerSetRsiState((uid, sprite), 0, $"scorch{_random.Next(1, 4)}");
        }

        _scorches.Add((uid, now));
        while (_scorches.Count > MaxScorches)
        {
            var old = _scorches[0].Uid;
            _scorches.RemoveAt(0);
            if (!TerminatingOrDeleted(old))
                QueueDel(old);
        }
    }

    private void UpdateScorches(float now)
    {
        for (var i = _scorches.Count - 1; i >= 0; i--)
        {
            var (uid, born) = _scorches[i];
            var age = now - born;

            if (TerminatingOrDeleted(uid) || age >= ScorchLife)
            {
                if (!TerminatingOrDeleted(uid))
                    QueueDel(uid);

                _scorches.RemoveAt(i);
                continue;
            }

            var fade = Math.Clamp((ScorchLife - age) / ScorchFade, 0f, 1f);
            if (fade < 1f && TryComp<SpriteComponent>(uid, out var sprite))
                _sprite.SetColor((uid, sprite), Color.White.WithAlpha(fade));
        }
    }

    private void ApplyColors(BMExplosionEffect fx, string type)
    {
        fx.Hot = DefaultHot;
        fx.Mid = DefaultMid;
        fx.Late = DefaultLate;
        fx.Smoke = DefaultSmoke;
        fx.LightColor = Color.Orange;

        if (!_proto.TryIndex(type, out ExplosionPrototype? proto))
            return;

        fx.LightColor = proto.LightColor;
        if (proto.FireColor is not { } fire)
            return;

        fx.Hot = Color.InterpolateBetween(fire, Color.White, 0.6f).WithAlpha(1f);
        fx.Mid = fire.WithAlpha(1f);
        fx.Late = Color.InterpolateBetween(fire, Color.Black, 0.6f).WithAlpha(1f);
        fx.Smoke = Color.InterpolateBetween(DefaultSmoke, fire, 0.15f).WithAlpha(1f);
    }

    private void AddParticle(BMParticle particle, BMExplosionEffect fx)
    {
        if (_particles >= MaxParticles)
            return;

        fx.Particles.Add(particle);
        _particles++;
    }

    private void DeleteLight(BMExplosionEffect fx)
    {
        if (fx.Light.IsValid() && !TerminatingOrDeleted(fx.Light))
            QueueDel(fx.Light);

        fx.Light = EntityUid.Invalid;
    }

    private Vector2 RandomDir()
    {
        var a = _random.NextFloat(MathF.Tau);
        return new Vector2(MathF.Cos(a), MathF.Sin(a));
    }

    private static int Count(Dictionary<int, List<Vector2i>> sets)
    {
        var n = 0;
        foreach (var list in sets.Values)
        {
            n += list.Count;
        }

        return n;
    }
}
