using System.Linq;
using System.Numerics;
using Content.Shared._BlackM.Gib;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.Gib;

public sealed class GibParticleOverlay : Overlay
{
    [Dependency] private readonly IRobustRandom _random = default!;

    private const string Path = "/Textures/_BlackM/Effects/Gib/";
    private const int MaxParticles = 1400;
    private const float Gravity = 18f;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly Texture[][] _burst;
    private readonly Texture[][] _nova;
    private readonly Texture[][] _mist;
    private readonly Texture[] _chunks;
    private readonly Texture[] _drops;
    private readonly Texture[] _streaks;

    private readonly IGibWorld _world;
    private readonly List<Particle> _particles = new();

    private enum Kind { Burst, Nova, Mist, Chunk, Drop, Streak, Stain }

    private sealed class Particle
    {
        public Kind Kind;
        public MapId Map;
        public Vector2 Pos, Vel;
        public float Z, VZ;
        public bool Falls;
        public int Bounces;
        public float Rise;
        public float Age, Life, Delay;
        public float Size;
        public float Rot, Spin;
        public float Drag;
        public float Alpha = 1f;
        public Texture[]? Frames;
        public Texture? Tex;
        public Color Color;
        public Color Blood;
        public bool FadeIn;
        public bool Resting;
        public float DripTimer;
        public float Vis = -1f;
        public int TileX = int.MinValue, TileY = int.MinValue;
    }

    public GibParticleOverlay(IResourceCache cache, IGibWorld world)
    {
        IoCManager.InjectDependencies(this);
        _world = world;

        RSI Load(string name) => cache.GetResource<RSIResource>(Path + name).RSI;
        Texture[] Anim(RSI rsi, string state) => rsi[state].GetFrames(default);

        var burst = Load("burst.rsi");
        _burst = Enumerable.Range(1, 3).Select(i => Anim(burst, $"burst{i}")).ToArray();

        var nova = Load("nova.rsi");
        _nova = Enumerable.Range(1, 2).Select(i => Anim(nova, $"nova{i}")).ToArray();

        var mist = Load("mist.rsi");
        _mist = Enumerable.Range(1, 4).Select(i => Anim(mist, $"mist{i}")).ToArray();

        var chunks = Load("chunks.rsi");
        _chunks = Enumerable.Range(1, 4).Select(i => Anim(chunks, $"meat{i}")[0])
            .Concat(Enumerable.Range(1, 2).Select(i => Anim(chunks, $"organ{i}")[0]))
            .Concat(Enumerable.Range(1, 3).Select(i => Anim(chunks, $"bone{i}")[0]))
            .ToArray();

        var drops = Load("drops.rsi");
        _drops = Enumerable.Range(1, 3).Select(i => Anim(drops, $"drop{i}")[0]).ToArray();

        var streaks = Load("streaks.rsi");
        _streaks = Enumerable.Range(1, 2).Select(i => Anim(streaks, $"streak{i}")[0]).ToArray();
    }

    public void Burst(GibBurstEvent ev)
    {
        var s = MathF.Max(ev.Strength, 0.2f);
        var reach = ev.Reach;
        var map = ev.Map;
        var pos = ev.Position;
        var blood = ev.Color;
        var tint = Boost(blood);

        Add(new Particle
        {
            Kind = Kind.Burst, Map = map, Pos = pos, Life = 0.35f, Size = 3.4f * s * reach,
            Rot = _random.NextFloat(6.28f), Frames = _random.Pick(_burst), Color = tint,
        });

        if (ev.Nova)
        {
            Add(new Particle
            {
                Kind = Kind.Nova, Map = map, Pos = pos, Life = 0.48f, Delay = 0.02f, Size = 4.4f * s * reach,
                Rot = _random.NextFloat(6.28f), Frames = _random.Pick(_nova), Color = tint,
            });
        }

        for (var i = 0; i < (int) (6 * s); i++)
        {
            var dir = _random.NextAngle().ToVec();
            Add(new Particle
            {
                Kind = Kind.Mist, Map = map, Pos = pos + dir * _random.NextFloat(0.1f, 0.5f),
                Vel = dir * _random.NextFloat(0.5f, 2.0f) * reach, Drag = 2.4f,
                Life = _random.NextFloat(1.0f, 1.8f), Size = _random.NextFloat(0.9f, 1.6f) * reach,
                Rot = _random.NextFloat(6.28f), Spin = _random.NextFloat(-0.8f, 0.8f), Rise = 0.28f,
                Frames = _random.Pick(_mist), Alpha = 0.55f, Color = Shade(tint, 0.75f, 1f),
            });
        }

        if (ev.Chunks)
        {
            for (var i = 0; i < (int) (9 * s); i++)
            {
                Add(new Particle
                {
                    Kind = Kind.Chunk, Map = map, Pos = pos,
                    Vel = _random.NextAngle().ToVec() * _random.NextFloat(1.2f, 3.6f) * reach * s,
                    Z = _random.NextFloat(0.15f, 0.45f), VZ = _random.NextFloat(3.2f, 6.4f), Falls = true, Bounces = 1,
                    Drag = 0.6f, Life = 8f, Size = _random.NextFloat(0.40f, 0.58f),
                    Rot = _random.NextFloat(6.28f), Spin = _random.NextFloat(-14f, 14f),
                    Tex = _random.Pick(_chunks), Color = Color.White, Blood = blood,
                    DripTimer = _random.NextFloat(0.07f),
                });
            }
        }

        for (var i = 0; i < (int) (55 * s); i++)
        {
            var dir = _random.NextAngle().ToVec();
            AddDrop(map, pos + dir * _random.NextFloat(0.15f),
                dir * _random.NextFloat(0.8f, 3.8f) * reach * s,
                _random.NextFloat(0.1f, 0.5f), _random.NextFloat(1.2f, 5f), tint);
        }

        for (var i = 0; i < (int) (22 * s); i++)
        {
            var dir = _random.NextAngle().ToVec();
            AddDrop(map, pos + dir * _random.NextFloat(0.1f),
                dir * _random.NextFloat(0.15f, 1.3f) * reach,
                _random.NextFloat(0.15f, 0.5f), _random.NextFloat(6.5f, 10.5f), tint);
        }

        for (var i = 0; i < (int) (30 * s); i++)
        {
            var offset = _random.NextAngle().ToVec() * 1.5f * reach * MathF.Sqrt(_random.NextFloat());
            var p = AddDrop(map, pos + offset, Vector2.Zero, _random.NextFloat(1.4f, 2.8f),
                -_random.NextFloat(0.5f, 2.5f), tint);
            if (p != null)
            {
                p.Delay = _random.NextFloat(0.2f, 1.0f);
                p.FadeIn = true;
            }
        }

        for (var i = 0; i < (int) (12 * s); i++)
        {
            var dir = _random.NextAngle().ToVec();
            Add(new Particle
            {
                Kind = Kind.Streak, Map = map, Pos = pos, Vel = dir * _random.NextFloat(5f, 10f) * reach * s,
                Drag = 6f, Life = _random.NextFloat(0.15f, 0.30f), Size = _random.NextFloat(0.35f, 0.7f),
                Rot = MathF.Atan2(dir.Y, dir.X), Tex = _random.Pick(_streaks), Color = Shade(tint, 0.8f, 1f),
            });
        }
    }

    public void Drip(MapId map, Vector2 pos, Color blood)
    {
        AddDrop(map, pos, Vector2.Zero, _random.NextFloat(0.12f, 0.3f), 0f, Boost(blood), _random.NextFloat(0.05f, 0.1f));
    }

    private Particle? AddDrop(MapId map, Vector2 pos, Vector2 vel, float z, float vz, Color tint, float? size = null)
    {
        var p = new Particle
        {
            Kind = Kind.Drop, Map = map, Pos = pos, Vel = vel, Z = z, VZ = vz, Falls = true,
            Drag = 0.5f, Life = 6f, Size = size ?? _random.NextFloat(0.07f, 0.15f),
            Tex = _random.Pick(_drops), Color = Shade(tint, 0.7f, 1f),
        };

        return Add(p) ? p : null;
    }

    private void AddStain(MapId map, Vector2 pos, Color color, float size)
    {
        Add(new Particle
        {
            Kind = Kind.Stain, Map = map, Pos = pos, Life = _random.NextFloat(3.5f, 6.5f),
            Size = MathF.Max(size * 1.5f, 0.1f), Rot = _random.NextFloat(6.28f),
            Tex = _random.Pick(_drops), Color = new Color(color.R * 0.62f, color.G * 0.62f, color.B * 0.62f, 1f),
        });
    }

    private bool Add(Particle p)
    {
        if (_particles.Count >= MaxParticles)
            return false;

        _particles.Add(p);
        return true;
    }

    private static Color Boost(Color c)
        => new(MathF.Min(c.R * 1.8f, 1f), MathF.Min(c.G * 1.8f, 1f), MathF.Min(c.B * 1.8f, 1f), 1f);

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

            var target = _world.Visible(p.Map, p.Pos) ? 1f : 0f;
            p.Vis = p.Vis < 0f ? target : p.Vis + Math.Clamp(target - p.Vis, -dt * 7f, dt * 7f);

            if (p.Resting)
                continue;

            p.Rot += p.Spin * dt;
            var oldPos = p.Pos;
            p.Pos += p.Vel * dt;
            p.Vel *= MathF.Max(0f, 1f - p.Drag * dt);

            if (p.Vel.LengthSquared() > 0.01f && p.Kind is Kind.Mist or Kind.Drop or Kind.Chunk or Kind.Streak)
            {
                var tx = (int) MathF.Floor(p.Pos.X);
                var ty = (int) MathF.Floor(p.Pos.Y);
                if (tx != p.TileX || ty != p.TileY)
                {
                    p.TileX = tx;
                    p.TileY = ty;

                    if (_world.Blocked(p.Map, p.Pos))
                    {
                        p.Pos = oldPos;
                        p.Vel = Vector2.Zero;

                        if (p.Kind == Kind.Streak)
                        {
                            p.Life = p.Age;
                            continue;
                        }

                        if (p.Kind == Kind.Mist)
                            p.Life = MathF.Min(p.Life, p.Age + 0.4f);
                        else
                            p.VZ = MathF.Min(p.VZ, 0f);
                    }
                }
            }

            if (p.Rise != 0f)
                p.Z += p.Rise * dt;

            if (p.Kind == Kind.Chunk && !p.Resting)
            {
                p.DripTimer -= dt;
                if (p.DripTimer <= 0f && p.Z > 0.05f)
                {
                    p.DripTimer = 0.07f;
                    AddDrop(p.Map, p.Pos, Vector2.Zero, p.Z, 0f, Boost(p.Blood), _random.NextFloat(0.05f, 0.09f));
                }
            }

            if (!p.Falls)
                continue;

            p.VZ -= Gravity * dt;
            p.Z += p.VZ * dt;

            if (p.Z > 0f)
                continue;

            p.Z = 0f;

            if (p.Kind == Kind.Chunk)
            {
                AddStain(p.Map, p.Pos, Boost(p.Blood), 0.14f);

                if (p.Bounces > 0)
                {
                    p.Bounces--;
                    p.VZ = -p.VZ * 0.42f;
                    p.Vel *= 0.55f;
                    p.Spin *= 0.5f;
                }
                else
                {
                    p.Falls = false;
                    p.Resting = true;
                    p.Vel = Vector2.Zero;
                    p.Spin = 0f;
                    p.Age = 0f;
                    p.Life = 2.4f;
                }
            }
            else if (p.Kind == Kind.Drop)
            {
                p.Kind = Kind.Stain;
                p.Falls = false;
                p.Vel = Vector2.Zero;
                p.Z = 0f;
                p.Age = 0f;
                p.Life = _random.NextFloat(3.5f, 6.5f);
                p.Size = MathF.Max(p.Size * 1.5f, 0.1f);
                p.Rot = _random.NextFloat(6.28f);
                p.Color = new Color(p.Color.R * 0.62f, p.Color.G * 0.62f, p.Color.B * 0.62f, 1f);
                p.FadeIn = false;
            }
            else
            {
                p.Life = p.Age;
            }
        }
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_particles.Count == 0)
            return;

        var handle = args.WorldHandle;
        var view = args.WorldAABB.Enlarged(5f);

        DrawPass(handle, view, args.MapId, static k => k is Kind.Stain or Kind.Nova);
        DrawPass(handle, view, args.MapId, static k => k == Kind.Mist);
        DrawPass(handle, view, args.MapId, static k => k == Kind.Burst);
        DrawPass(handle, view, args.MapId, static k => k is Kind.Chunk or Kind.Drop or Kind.Streak);

        handle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawPass(DrawingHandleWorld handle, Box2 view, MapId map, Func<Kind, bool> pick)
    {
        foreach (var p in _particles)
        {
            if (p.Delay > 0f || !pick(p.Kind) || p.Map != map || p.Vis <= 0.01f || !view.Contains(p.Pos))
                continue;

            DrawParticle(handle, p);
        }
    }

    private void DrawParticle(DrawingHandleWorld handle, Particle p)
    {
        var t = Math.Clamp(p.Age / p.Life, 0f, 1f);
        var drawPos = p.Pos + new Vector2(0f, p.Z * 0.55f);
        var vis = p.Vis;

        switch (p.Kind)
        {
            case Kind.Burst:
            case Kind.Nova:
                DrawAnim(handle, p.Frames!, t, drawPos, p.Rot, new Vector2(p.Size), p.Color, vis);
                break;

            case Kind.Mist:
                DrawAnim(handle, p.Frames!, t, drawPos, p.Rot, new Vector2(p.Size), p.Color, p.Alpha * vis);
                break;

            case Kind.Chunk:
            {
                var shadow = Math.Clamp(0.30f - p.Z * 0.08f, 0.05f, 0.30f) * vis * (p.Resting ? 1f - Smooth(0.6f, 1f, t) : 1f);
                handle.SetTransform(Matrix3x2.Identity);
                handle.DrawCircle(p.Pos + new Vector2(0f, -0.02f), p.Size * 0.32f, new Color(0f, 0f, 0f, shadow));

                var alpha = (p.Resting ? 1f - Smooth(0.6f, 1f, t) : 1f) * vis;
                DrawTex(handle, p.Tex!, drawPos, p.Rot, SpriteSize(p.Tex!, p.Size), Color.White.WithAlpha(alpha));
                break;
            }

            case Kind.Drop:
            {
                var speed = MathF.Sqrt(p.Vel.LengthSquared() + p.VZ * p.VZ * 0.3f);
                var rot = MathF.Atan2(p.Vel.Y + p.VZ * 0.55f, p.Vel.X);
                var size = SpriteSize(p.Tex!, p.Size);
                size.X *= 1f + Math.Clamp(speed * 0.14f, 0f, 1.6f);
                var alpha = vis * (p.FadeIn ? Math.Clamp(p.Age / 0.08f, 0f, 1f) : 1f);
                DrawTex(handle, p.Tex!, drawPos, rot, size, Tint(p.Color, alpha));
                break;
            }

            case Kind.Streak:
            {
                var len = p.Size;
                var dir = new Vector2(MathF.Cos(p.Rot), MathF.Sin(p.Rot));
                DrawTex(handle, p.Tex!, drawPos - dir * (len * 0.5f), p.Rot, new Vector2(len, len * 0.25f),
                    Tint(p.Color, (1f - t) * vis));
                break;
            }

            case Kind.Stain:
            {
                var alpha = vis * (1f - Smooth(0.65f, 1f, t));
                var size = SpriteSize(p.Tex!, p.Size);
                size.X *= 1.35f;
                size.Y *= 0.85f;
                DrawTex(handle, p.Tex!, p.Pos, p.Rot, size, Tint(p.Color, alpha));
                break;
            }
        }
    }

    private static Vector2 SpriteSize(Texture tex, float size)
    {
        var tw = tex.Width / (float) EyeManager.PixelsPerMeter;
        var th = tex.Height / (float) EyeManager.PixelsPerMeter;
        var k = size / MathF.Max(tw, th);
        return new Vector2(tw * k, th * k);
    }

    private static float Smooth(float a, float b, float x)
    {
        var t = Math.Clamp((x - a) / (b - a), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static Color Tint(Color c, float alpha)
        => new(c.R, c.G, c.B, Math.Clamp(alpha, 0f, 1f));

    private static void DrawAnim(DrawingHandleWorld handle, Texture[] frames, float t, Vector2 center, float rot,
        Vector2 size, Color color, float alpha)
    {
        var i = Math.Clamp((int) (Math.Clamp(t, 0f, 1f) * frames.Length), 0, frames.Length - 1);
        DrawTex(handle, frames[i], center, rot, size, Tint(color, alpha));
    }

    private static void DrawTex(DrawingHandleWorld handle, Texture tex, Vector2 center, float rot, Vector2 size, Color color)
    {
        if (color.A <= 0.01f)
            return;

        handle.SetTransform(Matrix3Helpers.CreateTransform(center, rot));
        handle.DrawTextureRect(tex, Box2.CenteredAround(Vector2.Zero, size), color);
    }
}
