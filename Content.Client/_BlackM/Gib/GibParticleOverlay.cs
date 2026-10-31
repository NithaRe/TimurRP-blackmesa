using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.Gib;

public sealed class GibParticleOverlay : Overlay
{
    [Dependency] private readonly IRobustRandom _random = default!;

    private const string Path = "/Textures/_BlackM/Effects/Gib/Particles/";
    private const int MaxParticles = 800;
    private const float Gravity = 18f;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly Texture _drop, _streak, _blob, _mist;
    private readonly List<Particle> _particles = new();

    private enum Kind { Mist, Chunk, Drop, Streak, Stain }

    private sealed class Particle
    {
        public Kind Kind;
        public MapId Map;
        public Vector2 Pos, Vel;
        public float Z, VZ;
        public float Age, Life;
        public float Size, EndSize;
        public float Rot, Spin;
        public float Drag;
        public float Alpha = 1f;
        public bool Falls;
        public bool Landed;
        public float Delay;
        public bool FadeIn;
        public Color Color;
    }

    public GibParticleOverlay(IResourceCache cache)
    {
        IoCManager.InjectDependencies(this);
        _drop = cache.GetResource<TextureResource>(Path + "drop.png").Texture;
        _streak = cache.GetResource<TextureResource>(Path + "streak.png").Texture;
        _blob = cache.GetResource<TextureResource>(Path + "blob.png").Texture;
        _mist = cache.GetResource<TextureResource>(Path + "mist.png").Texture;
    }

    public void Burst(MapId map, Vector2 pos, float strength, Color color)
    {
        strength = Math.Max(strength, 0.2f);

        for (var i = 0; i < (int) (7 * strength); i++)
        {
            var dir = _random.NextAngle().ToVec();
            Add(new Particle
            {
                Kind = Kind.Mist, Map = map, Pos = pos + dir * _random.NextFloat(0.15f),
                Vel = dir * _random.NextFloat(0.3f, 1.1f), Drag = 3f,
                Life = _random.NextFloat(0.45f, 0.9f),
                Size = _random.NextFloat(0.6f, 0.9f), EndSize = _random.NextFloat(1.2f, 1.8f),
                Rot = _random.NextFloat(6.28f), Spin = _random.NextFloat(-0.8f, 0.8f),
                Color = Shade(color, 0.8f, 1.0f), Alpha = 0.22f,
            });
        }

        for (var i = 0; i < (int) (8 * strength); i++)
        {
            var dir = _random.NextAngle().ToVec();
            Add(new Particle
            {
                Kind = Kind.Chunk, Map = map, Pos = pos,
                Vel = dir * _random.NextFloat(1.0f, 2.6f) * strength,
                Z = _random.NextFloat(0.15f, 0.5f), VZ = _random.NextFloat(2.5f, 4.5f), Falls = true,
                Drag = 0.5f, Life = 4f,
                Size = _random.NextFloat(0.16f, 0.28f),
                Rot = _random.NextFloat(6.28f), Spin = _random.NextFloat(-6f, 6f),
                Color = Shade(color, 0.7f, 1.0f),
            });
        }

        for (var i = 0; i < (int) (50 * strength); i++)
        {
            var dir = _random.NextAngle().ToVec();
            Add(new Particle
            {
                Kind = Kind.Drop, Map = map, Pos = pos + dir * _random.NextFloat(0.15f),
                Vel = dir * _random.NextFloat(0.8f, 3.6f) * strength,
                Z = _random.NextFloat(0.1f, 0.5f), VZ = _random.NextFloat(1.2f, 4.2f), Falls = true,
                Drag = 0.5f, Life = 4f,
                Size = _random.NextFloat(0.07f, 0.15f),
                Color = Shade(color, 0.7f, 1.0f),
            });
        }

        for (var i = 0; i < (int) (22 * strength); i++)
        {
            var dir = _random.NextAngle().ToVec();
            Add(new Particle
            {
                Kind = Kind.Drop, Map = map, Pos = pos + dir * _random.NextFloat(0.1f),
                Vel = dir * _random.NextFloat(0.15f, 1.3f),
                Z = _random.NextFloat(0.15f, 0.5f), VZ = _random.NextFloat(6.5f, 10.5f), Falls = true,
                Drag = 0.3f, Life = 4f,
                Size = _random.NextFloat(0.08f, 0.17f),
                Color = Shade(color, 0.7f, 1.0f),
            });
        }

        for (var i = 0; i < (int) (3 * strength); i++)
        {
            var dir = _random.NextAngle().ToVec();
            Add(new Particle
            {
                Kind = Kind.Chunk, Map = map, Pos = pos,
                Vel = dir * _random.NextFloat(0.3f, 1.4f),
                Z = 0.3f, VZ = _random.NextFloat(5.5f, 8f), Falls = true,
                Drag = 0.3f, Life = 4f,
                Size = _random.NextFloat(0.18f, 0.30f),
                Rot = _random.NextFloat(6.28f), Spin = _random.NextFloat(-8f, 8f),
                Color = Shade(color, 0.7f, 1.0f),
            });
        }

        for (var i = 0; i < (int) (30 * strength); i++)
        {
            var offset = _random.NextAngle().ToVec() * 1.5f * MathF.Sqrt(_random.NextFloat());
            Add(new Particle
            {
                Kind = Kind.Drop, Map = map, Pos = pos + offset,
                Vel = Vector2.Zero,
                Z = _random.NextFloat(1.4f, 2.8f), VZ = -_random.NextFloat(0.5f, 2.5f), Falls = true,
                Drag = 0f, Life = 4f, Delay = _random.NextFloat(0.2f, 1.0f), FadeIn = true,
                Size = _random.NextFloat(0.07f, 0.14f),
                Color = Shade(color, 0.7f, 1.0f),
            });
        }

        for (var i = 0; i < (int) (8 * strength); i++)
        {
            var dir = _random.NextAngle().ToVec();
            Add(new Particle
            {
                Kind = Kind.Streak, Map = map, Pos = pos, Vel = dir * _random.NextFloat(4f, 8f) * strength,
                Drag = 7f, Life = _random.NextFloat(0.14f, 0.26f),
                Size = _random.NextFloat(0.30f, 0.55f),
                Rot = MathF.Atan2(dir.Y, dir.X),
                Color = Shade(color, 0.8f, 1.0f),
            });
        }
    }

    private void Add(Particle p)
    {
        if (_particles.Count < MaxParticles)
            _particles.Add(p);
    }

    private Color Shade(Color c, float lo, float hi)
    {
        var s = _random.NextFloat(lo, hi);
        return new Color(c.R * s, c.G * s, c.B * s, c.A);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        var dt = args.DeltaSeconds;

        for (var i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];

            if (p.Delay > 0f)
            {
                p.Delay -= dt;
                continue;
            }

            p.Age += dt;

            if (p.Age >= p.Life)
            {
                _particles.RemoveAt(i);
                continue;
            }

            p.Rot += p.Spin * dt;

            if (p.Landed)
                continue;

            p.Pos += p.Vel * dt;
            p.Vel *= MathF.Max(0f, 1f - p.Drag * dt);

            if (p.Falls)
            {
                p.VZ -= Gravity * dt;
                p.Z += p.VZ * dt;

                if (p.Z <= 0f)
                {
                    p.Z = 0f;
                    p.Landed = true;
                    p.Vel = Vector2.Zero;
                    p.Spin = 0f;
                    p.Age = 0f;
                    p.Life = _random.NextFloat(2.5f, 5f);
                    p.Size = p.Kind == Kind.Chunk ? p.Size * 1.3f : MathF.Max(p.Size * 1.2f, 0.09f);
                    p.Kind = Kind.Stain;
                    p.Color = new Color(p.Color.R * 0.7f, p.Color.G * 0.7f, p.Color.B * 0.7f, p.Color.A);
                }
            }
        }
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_particles.Count == 0)
            return;

        var handle = args.WorldHandle;

        foreach (var p in _particles)
        {
            if (p.Map != args.MapId || p.Delay > 0f)
                continue;

            var t = Math.Clamp(p.Age / p.Life, 0f, 1f);
            var size = p.EndSize > 0f ? MathHelper.Lerp(p.Size, p.EndSize, t) : p.Size;
            var alpha = p.Alpha * p.Color.A;
            if (p.FadeIn && !p.Landed)
                alpha *= Math.Clamp(p.Age / 0.08f, 0f, 1f);
            Texture tex;
            var rot = p.Rot;
            var stretch = new Vector2(1f, 1f);

            switch (p.Kind)
            {
                case Kind.Mist:
                    tex = _mist; alpha *= MathF.Sin(MathF.PI * MathF.Pow(t, 0.6f)); break;
                case Kind.Chunk:
                    tex = _blob; break;
                case Kind.Drop:
                {
                    tex = _drop;
                    var speed = MathF.Sqrt(p.Vel.LengthSquared() + p.VZ * p.VZ * 0.3f);
                    rot = MathF.Atan2(p.Vel.Y + p.VZ * 0.55f, p.Vel.X);
                    stretch = new Vector2(1f + Math.Clamp(speed * 0.14f, 0f, 1.6f), 1f);
                    break;
                }
                case Kind.Streak:
                    tex = _streak; alpha *= 1f - t * t; break;
                default: // Stain
                    tex = _drop;
                    alpha *= t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                    stretch = new Vector2(1.25f, 0.8f);
                    break;
            }

            if (alpha <= 0.01f)
                continue;

            var tw = tex.Width / (float) EyeManager.PixelsPerMeter;
            var th = tex.Height / (float) EyeManager.PixelsPerMeter;
            var k = size / MathF.Max(tw, th);
            var w = tw * k * stretch.X;
            var h = th * k * stretch.Y;

            var drawPos = p.Pos + new Vector2(0f, p.Z * 0.55f);
            handle.SetTransform(Matrix3Helpers.CreateTransform(drawPos, rot));
            handle.DrawTextureRect(tex, Box2.CenteredAround(Vector2.Zero, new Vector2(w, h)),
                new Color(p.Color.R, p.Color.G, p.Color.B, Math.Clamp(alpha, 0f, 1f)));
        }

        handle.SetTransform(Matrix3x2.Identity);
    }
}
