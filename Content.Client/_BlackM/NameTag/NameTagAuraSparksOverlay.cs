using System.Numerics;
using Content.Shared._BlackM.NameTag;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.NameTag;

public sealed class NameTagAuraSparksOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly SharedTransformSystem _transform;

    private readonly Texture _sparkle;
    private readonly Texture _dot;
    private readonly Texture _orb;

    private const float R = NameTagAuraState.Radius;
    private const float RingFrac = 0.9375f;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public NameTagAuraSparksOverlay()
    {
        IoCManager.InjectDependencies(this);
        _transform = _entMan.System<SharedTransformSystem>();

        _sparkle = LoadTex("sparkle");
        _dot = LoadTex("aura_dot");
        _orb = LoadTex("aura_orb");
    }

    private Texture LoadTex(string name)
    {
        return _cache.GetResource<TextureResource>($"/Textures/_BlackM/NameTag/{name}.png").Texture;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var t = _timing.RealTime.TotalSeconds;
        var view = args.WorldAABB.Enlarged(R * 1.4f);

        var query = _entMan.EntityQueryEnumerator<NameTagComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var tag, out var xform))
        {
            if (tag.AuraStyle == NameTagAuraStyle.None || xform.MapID != args.MapId)
                continue;

            var pos = _transform.GetWorldPosition(xform);
            if (!view.Contains(pos))
                continue;

            if (!NameTagAuraState.TryGet(uid, t, out var grow, out var alpha))
                continue;

            var seed = (uid.Id % 11) * 0.7;
            var bright = Color.InterpolateBetween(tag.Color, Color.White, 0.35f);

            switch (tag.AuraStyle)
            {
                case NameTagAuraStyle.Rune:
                    DrawRune(handle, pos, bright, grow, alpha, t, seed);
                    break;
                case NameTagAuraStyle.Flame:
                    DrawFlame(handle, pos, bright, grow, alpha, t, seed);
                    break;
                case NameTagAuraStyle.Orbit:
                    DrawOrbit(handle, pos, bright, grow, alpha, t, seed);
                    break;
            }
        }
    }

    private void DrawRune(DrawingHandleWorld h, Vector2 pos, Color bright, float grow, float alpha, double t, double seed)
    {
        var ringR = R * RingFrac * grow;
        DrawComets(h, pos, bright, ringR, alpha, t * 1.0 + seed, 2, 8, 0.12, 1.0);

        for (var i = 0; i < 6; i++)
        {
            var ang = i * Math.PI * 2 / 6 + t * 0.2 + seed;
            var tw = (float) Math.Pow(Math.Max(0.0, Math.Sin(t * 1.7 + i * 1.9)), 2.0);
            if (tw < 0.02f)
                continue;

            var p = pos + new Vector2((float) Math.Cos(ang), (float) Math.Sin(ang)) * (R * 0.8f * grow);
            var size = 0.38f * tw;
            h.DrawTextureRect(_sparkle, Box2.CenteredAround(p, new Vector2(size)), Color.White.WithAlpha(tw * alpha));
        }
    }

    private void DrawFlame(DrawingHandleWorld h, Vector2 pos, Color bright, float grow, float alpha, double t, double seed)
    {
        const int count = 16;
        for (var i = 0; i < count; i++)
        {
            var s0 = (i * 0.61803398875 + seed * 0.1) % 1.0;
            var life = (t * (0.35 + (i % 3) * 0.08) + s0) % 1.0;

            var ang = s0 * Math.PI * 2 + t * 0.12 + Math.Sin(t * 2.0 + i) * 0.12;
            var radius = R * (0.95 + 0.5 * life) * grow;

            var fade = (float) Math.Sin(Math.PI * life);
            var p = pos + new Vector2((float) Math.Cos(ang), (float) Math.Sin(ang)) * (float) radius;
            var col = bright.WithAlpha(fade * alpha);

            if (i % 4 == 0)
            {
                var s = 0.26f * (0.4f + 0.6f * fade);
                h.DrawTextureRect(_sparkle, Box2.CenteredAround(p, new Vector2(s)), col);
            }
            else
            {
                var s = 0.22f * (1f - 0.5f * (float) life);
                h.DrawTextureRect(_dot, Box2.CenteredAround(p, new Vector2(s)), col);
            }
        }
    }

    private void DrawOrbit(DrawingHandleWorld h, Vector2 pos, Color bright, float grow, float alpha, double t, double seed)
    {
        for (var i = 0; i < NameTagAuraOverlay.OrbitFracs.Length; i++)
        {
            var radius = R * NameTagAuraOverlay.OrbitFracs[i] * grow;
            var dir = i % 2 == 0 ? 1.0 : -1.0;
            var speed = 0.9 - 0.2 * i;
            var count = i + 1;

            for (var k = 0; k < count; k++)
            {
                var head = t * speed * dir + k * Math.PI * 2 / count + seed + i;

                for (var j = 6; j >= 1; j--)
                {
                    var ang = head - dir * j * 0.12;
                    var f = 1f - j / 7f;
                    var tp = pos + new Vector2((float) Math.Cos(ang), (float) Math.Sin(ang)) * radius;
                    h.DrawTextureRect(_dot, Box2.CenteredAround(tp, new Vector2(0.10f + 0.16f * f)),
                        bright.WithAlpha(f * f * 0.9f * alpha));
                }

                var hp = pos + new Vector2((float) Math.Cos(head), (float) Math.Sin(head)) * radius;
                h.DrawTextureRect(_orb, Box2.CenteredAround(hp, new Vector2(0.42f)), bright.WithAlpha(alpha));
            }
        }
    }

    private void DrawComets(
        DrawingHandleWorld h, Vector2 pos, Color bright, float ringR, float alpha,
        double headBase, int count, int trail, double step, double dir)
    {
        for (var c = 0; c < count; c++)
        {
            var head = headBase * 1.1 * dir + c * Math.PI * 2 / count;
            for (var j = 0; j < trail; j++)
            {
                var ang = head - dir * j * step;
                var k = 1f - j / (float) trail;
                var a = (float) Math.Pow(k, 1.5) * alpha;
                var p = pos + new Vector2((float) Math.Cos(ang), (float) Math.Sin(ang)) * ringR;

                if (j == 0)
                {
                    h.DrawTextureRect(_sparkle, Box2.CenteredAround(p, new Vector2(0.55f)), Color.White.WithAlpha(a));
                }
                else
                {
                    h.DrawTextureRect(_dot, Box2.CenteredAround(p, new Vector2(0.28f * k + 0.06f)),
                        bright.WithAlpha(a * 0.85f));
                }
            }
        }
    }
}