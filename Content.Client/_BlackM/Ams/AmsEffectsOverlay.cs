using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared._BlackM.Ams;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._BlackM.Ams;

public sealed class AmsEffectsOverlay : Overlay
{
    private readonly IEntityManager _entMan;
    private readonly IGameTiming _timing;
    private readonly SharedTransformSystem _xform;
    private readonly AmsEffectsSystem _fx;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV | OverlaySpace.WorldSpace;

    private static readonly ResPath FxRsi = new("/Textures/_BlackM/Effects/Ams/ams_fx.rsi");
    private const float PlasmaDelay = 0.08f;

    private readonly Texture _glow;
    private readonly Texture[] _plasma;

    private static readonly Color Cyan = Color.FromHex("#58dcff");
    private static readonly Color Ice = Color.FromHex("#c8f4ff");
    private static readonly Color Green = Color.FromHex("#6dff4a");
    private static readonly Color PaleGreen = Color.FromHex("#d8ffc4");

    private static readonly Vector2 BeamTop = new(0f, 0.44f);
    private static readonly Vector2 BeamBottom = new(0f, -1.13f);
    private static readonly Vector2 BeamMid = (BeamTop + BeamBottom) / 2f;
    private static readonly Vector2 LensTop = new(0f, 0.78f);
    private static readonly Vector2 LensBottom = new(0f, -1.41f);
    private static readonly Vector2[] Feeds = { new(-0.38f, 2.0f), new(-0.22f, 2.0f) };
    private const float FeedEndY = 1.5f;

    private static readonly Vector2[] Generators =
    {
        new(-1.48f, -0.35f), new(1.48f, -0.35f), new(-1.22f, -1.2f), new(1.22f, -1.2f),
    };

    private static readonly float[] GeneratorThresholds = { 0.05f, 0.15f, 0.25f, 0.35f };

    private static readonly Vector2[] Claws = { new(-0.38f, -0.44f), new(0.47f, 0.06f) };

    private readonly List<Vector2> _bolt = new(64);
    private Vector2[] _verts = new Vector2[384];
    private readonly Vector2[] _line = new Vector2[6];
    private readonly Rng _rng = new();

    private sealed class Rng
    {
        private uint _state = 1;

        public Rng Reset(int seed)
        {
            _state = (uint) seed | 1u;
            Next();
            Next();
            return this;
        }

        public float Next()
        {
            var x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return (x & 0xFFFFFF) / 16777216f;
        }

        public float Range(float a, float b) => a + (b - a) * Next();
    }

    public AmsEffectsOverlay(IEntityManager entMan, IGameTiming timing, AmsEffectsSystem fx)
    {
        _entMan = entMan;
        _timing = timing;
        _fx = fx;
        _xform = entMan.System<SharedTransformSystem>();

        var sprite = entMan.System<SpriteSystem>();
        _glow = sprite.Frame0(new SpriteSpecifier.Rsi(FxRsi, "glow"));

        var plasma = sprite.RsiStateLike(new SpriteSpecifier.Rsi(FxRsi, "plasma"));
        _plasma = new Texture[Math.Max(1, plasma.AnimationFrameCount)];
        for (var i = 0; i < _plasma.Length; i++)
            _plasma[i] = plasma.GetFrame(default, i);

        ZIndex = 5;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var now = _timing.CurTime;
        var t = (float) _timing.RealTime.TotalSeconds;

        if (args.Space == OverlaySpace.WorldSpace)
        {
            handle.SetTransform(Matrix3x2.Identity);
            DrawBlasts(handle, args, now);
            handle.SetTransform(Matrix3x2.Identity);
            return;
        }

        var bounds = args.WorldAABB.Enlarged(14f);
        var query = _entMan.EntityQueryEnumerator<AmsComponent, TransformComponent>();
        while (query.MoveNext(out _, out var ams, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (!bounds.Contains(_xform.GetWorldPosition(xform)))
                continue;

            handle.SetTransform(_xform.GetWorldMatrix(xform));

            switch (ams.Status)
            {
                case AmsStatus.Inactive when ams.Primed && ams.RequireSample:
                    DrawTarget(handle, ams.SampleOffset, t);
                    break;
                case AmsStatus.Synchronizing:
                    var elapsed = ams.SyncStartTime is { } start ? (float) (now - start).TotalSeconds : 0f;
                    DrawSync(handle, elapsed, MathF.Max(ams.ChargeTime, 1f), t);
                    break;
                case AmsStatus.Ready:
                    DrawSync(handle, ams.ChargeTime, MathF.Max(ams.ChargeTime, 1f), t);
                    break;
                case AmsStatus.Active:
                    DrawActive(handle, t);
                    break;
                case AmsStatus.Detonating:
                    DrawCascade(handle, AmsHelpers.GetDetonationProgress(ams, now), t);
                    break;
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    #region phases

    private void DrawTarget(DrawingHandleWorld h, Vector2 c, float t)
    {
        var pulse = 0.5f + 0.5f * MathF.Sin(t * 3f);
        var col = Cyan.WithAlpha(0.55f + 0.35f * pulse);

        h.DrawRect(Box2.CenteredAround(c, new Vector2(0.9f, 0.9f)), Cyan.WithAlpha(0.05f + 0.05f * pulse));

        const float s = 0.45f;
        const float arm = 0.18f;
        for (var i = 0; i < 4; i++)
        {
            var dx = i == 0 || i == 3 ? -1f : 1f;
            var dy = i < 2 ? -1f : 1f;
            var corner = c + new Vector2(dx * s, dy * s);
            Line(h, corner, corner - new Vector2(dx * arm, 0f), 0.05f, col);
            Line(h, corner, corner - new Vector2(0f, dy * arm), 0.05f, col);
        }

        var k = (t * 0.8f) % 1f;
        var half = 0.45f + 0.35f * (1f - k);
        Rect(h, c, half, 0.03f, Cyan.WithAlpha(0.5f * k));
    }

    private void DrawSync(DrawingHandleWorld h, float elapsed, float total, float t)
    {
        var c = Math.Clamp(elapsed / total, 0f, 1f);

        const float r0 = 0.7f;
        const float r1 = 4f;
        var e = MathF.Min(elapsed, total);
        var beatPhase = r0 * e + (r1 - r0) * e * e * e / (3f * total * total);
        var beat = (int) MathF.Floor(beatPhase);
        var u = beatPhase - beat; // 0..1 
        var hit = MathF.Max(0f, 1f - u / 0.35f);
        var hum = 0.85f + 0.15f * MathF.Sin(t * 7f);

        Glow(h, LensTop, 0.6f + 1.2f * c, Cyan.WithAlpha((0.12f + 0.4f * c) * hum));
        Glow(h, LensBottom, 0.6f + 1.2f * c, Cyan.WithAlpha((0.12f + 0.4f * c) * hum));
        Glow(h, LensTop, 0.3f, Ice.WithAlpha(0.2f + 0.5f * c + 0.2f * hit * c));
        Glow(h, LensBottom, 0.3f, Ice.WithAlpha(0.2f + 0.5f * c + 0.2f * hit * c));

        var pulses = 1 + (int) (3f * c);
        for (var f = 0; f < Feeds.Length; f++)
        {
            for (var i = 0; i < pulses; i++)
            {
                var k = (t * (0.6f + 1.4f * c) + (i + 0.5f * f) / pulses) % 1f;
                var p = new Vector2(Feeds[f].X, MathHelper.Lerp(Feeds[f].Y, FeedEndY, k));
                var a = MathF.Sin(k * MathF.PI);
                Glow(h, p, 0.3f, Cyan.WithAlpha(0.45f * a));
                Dot(h, p, 0.06f, Ice.WithAlpha(0.9f * a));
            }
        }

        for (var i = 0; i < Generators.Length; i++)
        {
            var thr = GeneratorThresholds[i];
            if (c < thr)
                continue;

            var since = elapsed - thr * total;
            var flash = MathF.Max(0f, 1f - since / 0.6f);
            var g = Generators[i];

            Glow(h, g, 0.9f, Cyan.WithAlpha((0.22f + 0.12f * hit) * hum));
            Glow(h, g, 0.35f, Ice.WithAlpha(0.35f + 0.25f * hit));

            if (flash > 0f)
            {
                Glow(h, g, 0.8f + 1.6f * (1f - flash), Ice.WithAlpha(0.8f * flash));
                Ring(h, g, 0.3f + 0.9f * (1f - flash), 0.04f, Ice.WithAlpha(0.8f * flash));
            }

            if (hit > 0f)
            {
                var rng = _rng.Reset(Hash(beat, i, 13));
                var a = rng.Range(0f, MathF.Tau);
                Bolt(h, g + Dir(a) * 0.3f, g + Dir(a + rng.Range(1f, 2f)) * 0.3f, 0.4f, rng, Cyan, 0.6f * hit);
            }
        }

        if (c >= 0.4f)
        {
            var k = (c - 0.4f) / 0.6f;
            for (var i = 0; i < Claws.Length; i++)
            {
                Glow(h, Claws[i], 0.4f + 0.5f * k, Cyan.WithAlpha((0.2f + 0.4f * k) * (0.7f + 0.3f * hit)));

                if (hit <= 0f || (beat + i) % 2 != 0 && c < 0.6f)
                    continue;

                var rng = _rng.Reset(Hash(beat, i, 31));
                for (var j = 0; j < 3; j++)
                {
                    var d = Dir(rng.Range(0f, MathF.Tau));
                    var p = Claws[i] + d * (0.05f + 0.25f * (1f - hit));
                    Dot(h, p, 0.05f, Ice.WithAlpha(hit));
                }
            }
        }

        if (c >= 0.6f && hit > 0f)
        {
            for (var i = 0; i < Claws.Length; i++)
            {
                if (c < 0.75f && (beat + i) % 2 != 0)
                    continue;

                var rng = _rng.Reset(Hash(beat, i, 47));
                var claw = Claws[i];
                var axis = new Vector2(0f, Math.Clamp(claw.Y + rng.Range(-0.2f, 0.2f), BeamBottom.Y, BeamTop.Y));
                Bolt(h, claw, axis, 0.3f, rng, Cyan, hit);
                Glow(h, axis, 0.4f, Ice.WithAlpha(0.6f * hit));
            }
        }

        if (c >= 0.8f && hit > 0f)
        {
            var rng = _rng.Reset(Hash(beat, 0, 59));
            Bolt(h, BeamTop, BeamBottom, 0.12f, rng, Cyan, hit);
            Glow(h, BeamTop, 0.6f, Ice.WithAlpha(0.6f * hit));
            Glow(h, BeamBottom, 0.6f, Ice.WithAlpha(0.6f * hit));
        }

        if (c >= 0.95f)
        {
            var k = (c - 0.95f) / 0.05f;
            Beam(h, BeamTop, BeamBottom, 0.5f * k * hum, Cyan, Ice);
        }
    }

    private void DrawActive(DrawingHandleWorld h, float t)
    {
        var flicker = 0.9f + 0.1f * MathF.Sin(t * 31f) * MathF.Sin(t * 11f);

        Halo(h, BeamTop, BeamBottom, flicker);
        Glow(h, LensTop, 1.2f, Ice.WithAlpha(0.45f * flicker));
        Glow(h, LensBottom, 1.2f, Ice.WithAlpha(0.45f * flicker));
        Glow(h, BeamMid, 3.2f, Cyan.WithAlpha(0.10f * flicker));

        var slot = (int) MathF.Floor(t * 10f);
        for (var i = 0; i < Claws.Length; i++)
        {
            var rng = _rng.Reset(Hash(slot, i, 23));
            var claw = Claws[i];
            var axis = new Vector2(0f, claw.Y + rng.Range(-0.12f, 0.12f));
            Bolt(h, claw, axis, 0.22f, rng, Cyan, 0.85f);
            Glow(h, claw, 0.45f, Ice.WithAlpha(0.5f * flicker));
            Glow(h, axis, 0.35f, Ice.WithAlpha(0.5f));
        }

        var big = (int) MathF.Floor(t * 1.5f);
        var bigU = t * 1.5f - big;
        if (bigU < 0.15f && _rng.Reset(Hash(big, 3, 71)).Next() < 0.6f)
            Bolt(h, BeamTop, BeamBottom, 0.08f, _rng, Ice, 1f - bigU / 0.15f);

        for (var i = 0; i < 10; i++)
        {
            var rng = _rng.Reset(Hash(i, 7, 61));
            var life = (t * rng.Range(0.3f, 0.6f) + rng.Next()) % 1f;
            var y = MathHelper.Lerp(BeamBottom.Y, BeamTop.Y, life);
            var x = rng.Range(-0.18f, 0.18f) * (0.5f + life);
            Dot(h, new Vector2(x, y), 0.05f, Ice.WithAlpha(0.8f * MathF.Sin(life * MathF.PI)));
        }
    }

    private void DrawCascade(DrawingHandleWorld h, float progress, float t)
    {
        var core = BeamMid;
        var size = 1.6f + 2.6f * progress + 0.15f * MathF.Sin(t * 9f);

        Glow(h, core, size * 3.2f, Green.WithAlpha(0.18f + 0.2f * progress));
        Glow(h, core, size * 1.6f, Green.WithAlpha(0.35f + 0.2f * progress));

        Beam(h, BeamTop, BeamBottom, 0.8f + 0.2f * MathF.Sin(t * 23f), Green, PaleGreen);

        var count = 6 + (int) (14f * progress);
        var reach = 2.5f + 9.5f * progress;
        for (var i = 0; i < count; i++)
        {
            var rng = Slot(t, 6f + 6f * progress, i, 47);
            if (rng.Next() > 0.55f + 0.4f * progress)
                continue;

            var dir = Dir(rng.Range(0f, MathF.Tau));
            var from = core + dir * size * 0.3f;
            var to = core + dir * (size * 0.4f + reach * rng.Range(0.45f, 1f));
            Bolt(h, from, to, 0.3f, rng, Green, 1f);
        }

        var frame = _plasma[(int) (t / PlasmaDelay) % _plasma.Length];
        Tex(h, frame, core, size, new Angle(t * 0.6f), Green.WithAlpha(0.9f));
        Tex(h, frame, core, size * 0.7f, new Angle(-t * 1.02f), PaleGreen.WithAlpha(0.8f));
        Glow(h, core, size * 0.6f, Color.White.WithAlpha(0.6f + 0.3f * progress));
    }

    private void DrawBlasts(DrawingHandleWorld h, in OverlayDrawArgs args, TimeSpan now)
    {
        foreach (var blast in _fx.Blasts)
        {
            if (blast.Map != args.MapId)
                continue;

            var age = (float) (now - blast.Start).TotalSeconds;
            if (age < 0f)
                continue;

            var radius = blast.Speed * age;

            if (age < 4f)
            {
                var k = 1f - age / 4f;
                Glow(h, blast.Epicenter, 6f + 30f * (1f - k * k), Color.White.WithAlpha(0.9f * k));
                Glow(h, blast.Epicenter, 4f + 12f * age, Green.WithAlpha(0.7f * k));
            }

            if (radius > blast.MaxRadius + 6f)
                continue;

            var view = args.WorldAABB.Enlarged(4f);
            var dist = (view.Center - blast.Epicenter).Length();
            var halfDiag = (view.TopRight - view.BottomLeft).Length() / 2f;
            if (MathF.Abs(dist - radius) > halfDiag + 3f)
                continue;

            var fade = Math.Clamp((blast.MaxRadius + 6f - radius) / 6f, 0f, 1f);
            Ring(h, blast.Epicenter, radius, 3.2f, Green.WithAlpha(0.10f * fade));
            Ring(h, blast.Epicenter, radius, 1.4f, Green.WithAlpha(0.28f * fade));
            Ring(h, blast.Epicenter, radius, 0.45f, PaleGreen.WithAlpha(0.75f * fade));
            Ring(h, blast.Epicenter, MathF.Max(0f, radius - 2.5f), 0.9f, Green.WithAlpha(0.15f * fade));
        }
    }

    #endregion

    #region primitives

    private Rng Slot(float t, float rate, int index, int salt)
    {
        var slot = (int) MathF.Floor(t * rate + index * 0.618f);
        return _rng.Reset(Hash(slot, index, salt));
    }

    private static Vector2 Dir(float a) => new(MathF.Cos(a), MathF.Sin(a));

    private static void Quad(Vector2[] v, int o, Vector2 a, Vector2 b, float width)
    {
        var d = b - a;
        var len = d.Length();
        var n = len > 0.0001f ? new Vector2(-d.Y, d.X) / len * (width / 2f) : new Vector2(width / 2f, 0f);

        v[o + 0] = a + n;
        v[o + 1] = a - n;
        v[o + 2] = b + n;
        v[o + 3] = b + n;
        v[o + 4] = a - n;
        v[o + 5] = b - n;
    }

    private void Line(DrawingHandleWorld h, Vector2 a, Vector2 b, float width, Color color)
    {
        Quad(_line, 0, a, b, width);
        h.DrawPrimitives(DrawPrimitiveTopology.TriangleList, new ReadOnlySpan<Vector2>(_line), color);
    }

    private void Polyline(DrawingHandleWorld h, List<Vector2> pts, float width, Color color)
    {
        var need = (pts.Count - 1) * 6;
        if (need <= 0)
            return;

        if (_verts.Length < need)
            _verts = new Vector2[need * 2];

        for (var i = 1; i < pts.Count; i++)
            Quad(_verts, (i - 1) * 6, pts[i - 1], pts[i], width);

        h.DrawPrimitives(DrawPrimitiveTopology.TriangleList, new ReadOnlySpan<Vector2>(_verts, 0, need), color);
    }

    private static void Dot(DrawingHandleWorld h, Vector2 c, float size, Color color)
    {
        h.DrawRect(Box2.CenteredAround(c, new Vector2(size, size)), color);
    }

    private void Rect(DrawingHandleWorld h, Vector2 c, float half, float width, Color color)
    {
        var bl = c + new Vector2(-half, -half);
        var br = c + new Vector2(half, -half);
        var tr = c + new Vector2(half, half);
        var tl = c + new Vector2(-half, half);
        Line(h, bl, br, width, color);
        Line(h, br, tr, width, color);
        Line(h, tr, tl, width, color);
        Line(h, tl, bl, width, color);
    }

    private void Ring(DrawingHandleWorld h, Vector2 c, float r, float width, Color color)
    {
        if (r <= 0.01f)
            return;

        var n = Math.Clamp((int) (r * 3f), 24, 360);
        _bolt.Clear();
        for (var i = 0; i <= n; i++)
            _bolt.Add(c + Dir(MathF.Tau * i / n) * r);

        Polyline(h, _bolt, width, color);
    }

    private void Beam(DrawingHandleWorld h, Vector2 a, Vector2 b, float intensity, Color color, Color core)
    {
        Halo(h, a, b, intensity, color);
        Line(h, a, b, 0.12f, core.WithAlpha(0.6f * intensity));
        Line(h, a, b, 0.05f, Color.White.WithAlpha(0.85f * intensity));
    }

    private void Halo(DrawingHandleWorld h, Vector2 a, Vector2 b, float intensity, Color? color = null)
    {
        var col = color ?? Cyan;
        Line(h, a, b, 0.80f, col.WithAlpha(0.05f * intensity));
        Line(h, a, b, 0.50f, col.WithAlpha(0.09f * intensity));
        Line(h, a, b, 0.28f, col.WithAlpha(0.16f * intensity));
    }

    private void Glow(DrawingHandleWorld h, Vector2 c, float size, Color color)
    {
        h.DrawTextureRect(_glow, Box2.CenteredAround(c, new Vector2(size, size)), color);
    }

    private static void Tex(DrawingHandleWorld h, Texture tex, Vector2 c, float size, Angle rot, Color color)
    {
        var box = Box2.CenteredAround(c, new Vector2(size, size));
        h.DrawTextureRect(tex, new Box2Rotated(box, rot, c), color);
    }

    private void Bolt(DrawingHandleWorld h, Vector2 from, Vector2 to, float roughness, Rng rng, Color color, float alpha)
    {
        _bolt.Clear();
        _bolt.Add(from);
        _bolt.Add(to);

        var len = (to - from).Length();
        var offset = len * roughness;
        var depth = Math.Clamp((int) MathF.Ceiling(MathF.Log(len * 5f + 1f) / 0.6931472f), 2, 5);

        for (var d = 0; d < depth; d++)
        {
            for (var i = _bolt.Count - 1; i > 0; i--)
            {
                var a = _bolt[i - 1];
                var b = _bolt[i];
                var dir = b - a;
                var normal = new Vector2(-dir.Y, dir.X);
                var nl = normal.Length();
                if (nl > 0f)
                    normal /= nl;

                _bolt.Insert(i, (a + b) / 2f + normal * rng.Range(-1f, 1f) * offset);
            }

            offset *= 0.5f;
        }

        Polyline(h, _bolt, 0.14f, color.WithAlpha(0.10f * alpha));
        Polyline(h, _bolt, 0.065f, color.WithAlpha(0.40f * alpha));
        Polyline(h, _bolt, 0.032f, Color.InterpolateBetween(color, Color.White, 0.75f).WithAlpha(0.95f * alpha));
    }

    private static int Hash(int a, int b, int c)
    {
        unchecked
        {
            var h = (uint) (a * 374761393 + b * 668265263 + c * 1274126177);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (int) (h ^ (h >> 16));
        }
    }

    #endregion
}
