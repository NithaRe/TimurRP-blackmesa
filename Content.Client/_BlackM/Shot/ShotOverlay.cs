using System.Numerics;
using Content.Shared._BlackM.Shot;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.Shot;

public sealed class ShotOverlay : Overlay
{
    [Dependency] private readonly IRobustRandom _random = default!;

    private const int MaxParticles = 300;
    private const int TracerSteps = 7;
    private static readonly float Px = 1f / EyeManager.PixelsPerMeter;

    private static readonly (float A, float L)[] MuzzleSpikes =
    {
        (-MathF.PI + 0.0f, 0.12f), (-2.4f, 0.18f), (-1.45f, 0.30f), (-0.72f, 0.52f), (-0.30f, 0.82f),
        (0f, 1.0f), (0.30f, 0.82f), (0.72f, 0.52f), (1.45f, 0.30f), (2.4f, 0.18f),
    };

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly Texture _smoke;
    private readonly Dictionary<EntityUid, ShotState> _states;
    private readonly List<Particle> _particles = new();
    private readonly Vector2[] _buf = new Vector2[128];
    private readonly Vector2[] _ring = new Vector2[64];

    private enum Kind { Flash, Spark, Smoke }

    private sealed class Particle
    {
        public Kind Kind;
        public MapId Map;
        public Vector2 Pos, Vel;
        public float Z, VZ;
        public bool Falls;
        public int Bounces;
        public float Age, Life;
        public float Size;
        public float Rot;
        public float Drag;
        public float Alpha = 1f;
        public Color Color, Color2, Color3;
        public (float A, float L)[]? Spikes;
        public float Valley = 0.24f;
        public float EndSize;
    }

    public ShotOverlay(IResourceCache cache, Dictionary<EntityUid, ShotState> states)
    {
        IoCManager.InjectDependencies(this);
        _states = states;
        _smoke = cache.GetResource<TextureResource>("/Textures/_BlackM/Effects/Shot/smoke.png").Texture;
    }

    public void Muzzle(MapId map, Vector2 pos, Vector2 dir, ShotComponent c)
    {
        var s = c.MuzzleScale;
        var angle = MathF.Atan2(dir.Y, dir.X) + _random.NextFloat(-0.07f, 0.07f);

        var spikes = new (float A, float L)[MuzzleSpikes.Length];
        for (var i = 0; i < spikes.Length; i++)
            spikes[i] = (MuzzleSpikes[i].A, MuzzleSpikes[i].L * _random.NextFloat(0.82f, 1.12f));

        Add(new Particle
        {
            Kind = Kind.Flash, Map = map, Pos = pos + dir * 0.10f, Rot = angle, Life = 0.065f,
            Size = 0.95f * s, Spikes = spikes, Valley = 0.24f,
            Color = c.TracerColor, Color2 = Color.InterpolateBetween(c.TracerColor, c.CoreColor, 0.6f), Color3 = c.CoreColor,
        });

        for (var i = 0; i < (int) MathF.Round(5 * s); i++)
        {
            var a = angle + _random.NextFloat(-0.38f, 0.38f);
            Add(new Particle
            {
                Kind = Kind.Spark, Map = map, Pos = pos + dir * 0.12f,
                Vel = new Vector2(MathF.Cos(a), MathF.Sin(a)) * _random.NextFloat(8f, 16f),
                Drag = 6f, Life = _random.NextFloat(0.08f, 0.16f), Size = _random.NextFloat(0.30f, 0.65f),
                Color = Color.InterpolateBetween(c.TracerColor, c.CoreColor, _random.NextFloat(0f, 0.7f)),
            });
        }

        if (c.Smoke)
        {
            for (var i = 0; i < 2; i++)
                AddSmoke(map, pos + dir * 0.3f, dir * _random.NextFloat(1f, 2.4f), 0.2f * s, 0.7f * s, 0.18f);
        }
    }

    public void Impact(MapId map, Vector2 pos, Vector2 dir, ShotComponent c)
    {
        var s = c.ImpactScale;
        var backAngle = MathF.Atan2(-dir.Y, -dir.X);

        var spikes = new (float A, float L)[8];
        for (var k = 0; k < 8; k++)
            spikes[k] = (-MathF.PI + k * MathF.PI / 4f, (k % 2 == 0 ? 1.0f : 0.62f) * _random.NextFloat(0.85f, 1.1f));

        Add(new Particle
        {
            Kind = Kind.Flash, Map = map, Pos = pos, Rot = _random.NextFloat(6.28f), Life = 0.055f,
            Size = 0.55f * s, Spikes = spikes, Valley = 0.22f,
            Color = c.TracerColor, Color2 = Color.InterpolateBetween(c.TracerColor, c.CoreColor, 0.6f), Color3 = c.CoreColor,
        });

        for (var i = 0; i < (int) MathF.Round(8 * s); i++)
        {
            var a = backAngle + _random.NextFloat(-1.2f, 1.2f);
            Add(new Particle
            {
                Kind = Kind.Spark, Map = map, Pos = pos,
                Vel = new Vector2(MathF.Cos(a), MathF.Sin(a)) * _random.NextFloat(2.2f, 6.5f),
                Z = _random.NextFloat(0.05f, 0.2f), VZ = _random.NextFloat(1f, 3.2f), Falls = true,
                Bounces = _random.Prob(0.4f) ? 1 : 0, Drag = 2.4f,
                Life = _random.NextFloat(0.3f, 0.65f), Size = _random.NextFloat(0.2f, 0.45f),
                Color = Color.InterpolateBetween(c.TracerColor, c.CoreColor, _random.NextFloat(0f, 0.7f)),
            });
        }

        if (c.Smoke)
        {
            for (var i = 0; i < 2; i++)
                AddSmoke(map, pos, new Vector2(-dir.X, -dir.Y) * _random.NextFloat(0.3f, 1f), 0.15f * s, 0.55f * s, 0.16f);
        }
    }

    private void AddSmoke(MapId map, Vector2 pos, Vector2 vel, float size, float endSize, float alpha)
    {
        Add(new Particle
        {
            Kind = Kind.Smoke, Map = map, Pos = pos, Vel = vel, Drag = 3f, Life = _random.NextFloat(0.5f, 0.9f),
            Size = size, Rot = _random.NextFloat(6.28f), Alpha = alpha, Color = new Color(0.78f, 0.78f, 0.8f),
            EndSize = endSize,
        });
    }

    private void Add(Particle p)
    {
        if (_particles.Count < MaxParticles)
            _particles.Add(p);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        var dt = args.DeltaSeconds;

        for (var i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Age += dt;

            if (p.Age >= p.Life)
            {
                _particles.RemoveAt(i);
                continue;
            }

            p.Pos += p.Vel * dt;
            p.Vel *= MathF.Max(0f, 1f - p.Drag * dt);

            if (!p.Falls)
                continue;

            p.VZ -= 9f * dt;
            p.Z += p.VZ * dt;

            if (p.Z > 0f)
                continue;

            if (p.Bounces > 0)
            {
                p.Bounces--;
                p.Z = 0f;
                p.VZ = -p.VZ * 0.5f;
                p.Vel *= 0.6f;
            }
            else
            {
                _particles.RemoveAt(i);
            }
        }
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var view = args.WorldAABB.Enlarged(4f);

        foreach (var st in _states.Values)
        {
            if (!st.Visible || st.Map != args.MapId || !view.Contains(st.Pos))
                continue;

            DrawTracer(handle, st);
        }

        foreach (var p in _particles)
        {
            if (p.Map != args.MapId || !view.Contains(p.Pos))
                continue;

            var t = Math.Clamp(p.Age / p.Life, 0f, 1f);
            var drawPos = p.Pos + new Vector2(0f, p.Z * 0.55f);

            switch (p.Kind)
            {
                case Kind.Flash:
                    DrawFlash(handle, p, drawPos, t);
                    break;

                case Kind.Spark:
                {
                    var rot = MathF.Atan2(p.Vel.Y + p.VZ * 0.55f, p.Vel.X);
                    var speed = MathF.Sqrt(p.Vel.LengthSquared() + p.VZ * p.VZ * 0.3f);
                    var len = MathF.Max(p.Size * 0.4f, p.Size * Math.Clamp(speed / 12f, 0.3f, 1f));
                    var a = 1f - t;
                    handle.SetTransform(Matrix3Helpers.CreateTransform(drawPos, rot));
                    handle.DrawRect(new Box2(-len, -Px * 0.75f, 0f, Px * 0.75f), Tint(p.Color, a));
                    break;
                }

                case Kind.Smoke:
                {
                    var size = MathHelper.Lerp(p.Size, p.EndSize, t);
                    var a = p.Alpha * MathF.Sin(MathF.PI * MathF.Pow(t, 0.55f));
                    handle.SetTransform(Matrix3Helpers.CreateTransform(drawPos, p.Rot));
                    handle.DrawTextureRect(_smoke, Box2.CenteredAround(Vector2.Zero, new Vector2(size)), Tint(p.Color, a));
                    break;
                }
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawTracer(DrawingHandleWorld handle, ShotState st)
    {
        var c = st.Comp;
        var angle = MathF.Atan2(st.Dir.Y, st.Dir.X);
        var len = MathF.Min(c.Length, st.Dist + 0.2f);
        var core = MathF.Max(Px * 1.5f, c.Width);
        var halo = core * 3f;
        var flick = 0.92f + 0.08f * MathF.Sin(st.Age * 60f + st.Phase);
        var seg = len / TracerSteps;

        handle.SetTransform(Matrix3Helpers.CreateTransform(st.Pos, angle));

        for (var i = 0; i < TracerSteps; i++)
        {
            var f = i / (float) (TracerSteps - 1);
            var fade = MathF.Pow(1f - f, 1.5f) + 0.05f;
            var taper = 1f - 0.55f * f;
            var x1 = -seg * i;
            var x0 = -seg * (i + 1);

            handle.DrawRect(new Box2(x0, -halo * taper * 0.5f, x1, halo * taper * 0.5f),
                c.TracerColor.WithAlpha(0.20f * fade * flick));

            var col = Color.InterpolateBetween(c.CoreColor, c.TracerColor, f * 0.85f);
            handle.DrawRect(new Box2(x0, -core * taper * 0.5f, x1, core * taper * 0.5f),
                col.WithAlpha(MathF.Min(1f, fade * 1.1f)));
        }

        handle.DrawRect(new Box2(-Px * 2f, -core * 0.75f, Px * 1.5f, core * 0.75f), c.CoreColor);
    }

    private void DrawFlash(DrawingHandleWorld handle, Particle p, Vector2 pos, float t)
    {
        if (p.Spikes == null)
            return;

        var shrink = 1f - 0.35f * t;
        var alpha = 1f - 0.5f * t;
        handle.SetTransform(Matrix3Helpers.CreateTransform(pos, p.Rot));

        DrawStar(handle, p.Spikes, p.Valley, p.Size * shrink, Tint(p.Color, alpha));
        DrawStar(handle, p.Spikes, p.Valley, p.Size * shrink * 0.62f, Tint(p.Color2, alpha));
        DrawStar(handle, p.Spikes, p.Valley, p.Size * shrink * 0.30f, Tint(p.Color3, alpha));
    }

    private void DrawStar(DrawingHandleWorld handle, (float A, float L)[] spikes, float valley, float size, Color color)
    {
        var n = spikes.Length;
        var points = 0;

        for (var k = 0; k < n; k++)
        {
            var (a, l) = spikes[k];
            var a2 = spikes[(k + 1) % n].A + (k == n - 1 ? MathF.Tau : 0f);
            var m = (a + a2) * 0.5f;

            _ring[points++] = new Vector2(MathF.Cos(a), MathF.Sin(a)) * l * size;
            _ring[points++] = new Vector2(MathF.Cos(m), MathF.Sin(m)) * valley * size;
        }

        var count = 0;
        for (var k = 0; k < points; k++)
        {
            _buf[count++] = Vector2.Zero;
            _buf[count++] = _ring[k];
            _buf[count++] = _ring[(k + 1) % points];
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _buf.AsSpan(0, count), color);
    }

    private static Color Tint(Color c, float alpha)
        => new(c.R, c.G, c.B, Math.Clamp(alpha, 0f, 1f));
}
