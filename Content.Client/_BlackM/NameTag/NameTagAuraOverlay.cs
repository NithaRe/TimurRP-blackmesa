using System.Numerics;
using Content.Shared._BlackM.NameTag;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.NameTag;

public sealed class NameTagAuraOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly SharedTransformSystem _transform;

    private readonly Texture _glow;
    private readonly Texture _wave;
    private readonly Texture _ringRunes;
    private readonly Texture _ringDash;
    private readonly Texture _orbit;
    private readonly Texture _petal;

    private readonly HashSet<EntityUid> _seen = new();
    private readonly List<EntityUid> _toRemove = new();

    private const float R = NameTagAuraState.Radius;
    private const float RingTex = 0.9375f;

    public static readonly float[] OrbitFracs = { 0.56f, 0.76f, 0.96f };

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

    public NameTagAuraOverlay()
    {
        IoCManager.InjectDependencies(this);
        _transform = _entMan.System<SharedTransformSystem>();

        _glow = LoadTex("aura_glow");
        _wave = LoadTex("aura_wave");
        _ringRunes = LoadTex("aura_ring_runes");
        _ringDash = LoadTex("aura_ring_dash");
        _orbit = LoadTex("aura_orbit");
        _petal = LoadTex("aura_petal");
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

        _seen.Clear();

        var query = _entMan.EntityQueryEnumerator<NameTagComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var tag, out var xform))
        {
            if (tag.AuraStyle == NameTagAuraStyle.None || xform.MapID != args.MapId)
                continue;

            _seen.Add(uid);

            var pos = _transform.GetWorldPosition(xform);
            if (!view.Contains(pos))
                continue;

            var grow = NameTagAuraState.Touch(uid, t, out var alpha);
            var seed = (uid.Id % 11) * 0.7;

            switch (tag.AuraStyle)
            {
                case NameTagAuraStyle.Rune:
                    DrawRune(handle, pos, tag.Color, grow, alpha, t, seed);
                    break;
                case NameTagAuraStyle.Flame:
                    DrawFlame(handle, pos, tag.Color, grow, alpha, t, seed);
                    break;
                case NameTagAuraStyle.Orbit:
                    DrawOrbit(handle, pos, tag.Color, grow, alpha, t, seed);
                    break;
            }
        }

        _toRemove.Clear();
        foreach (var key in NameTagAuraState.Tracked())
        {
            if (!_seen.Contains(key))
                _toRemove.Add(key);
        }

        foreach (var key in _toRemove)
            NameTagAuraState.Forget(key);
    }

    private void DrawRune(DrawingHandleWorld h, Vector2 pos, Color color, float grow, float alpha, double t, double seed)
    {
        var pulse = 0.5f + 0.5f * (float) Math.Sin(t * 2.0 + seed);
        var bright = Color.InterpolateBetween(color, Color.White, 0.3f);
        var spinIn = (1f - grow) * 5f;

        DrawFlat(h, _glow, pos, grow * (1f + 0.04f * pulse), color.WithAlpha((0.30f + 0.20f * pulse) * alpha));

        DrawRotated(h, _ringRunes, pos, grow, (float) (t * 0.25 + seed) + spinIn, bright.WithAlpha(0.95f * alpha));
        DrawRotated(h, _ringDash, pos, grow, (float) (-t * 0.5 - seed) - spinIn * 1.4f, color.WithAlpha(0.9f * alpha));

        var life = (t / 3.2 + seed * 0.1) % 1.0;
        var waveA = (float) Math.Pow(1.0 - life, 1.5) * 0.5f * (float) Math.Min(1.0, life * 8.0) * alpha;
        DrawFlat(h, _wave, pos, grow * (0.5f + 0.55f * (float) life), bright.WithAlpha(waveA));
    }

    private void DrawFlame(DrawingHandleWorld h, Vector2 pos, Color color, float grow, float alpha, double t, double seed)
    {
        var pulse = 0.5f + 0.5f * (float) Math.Sin(t * 2.6 + seed);
        var hot = Color.InterpolateBetween(color, Color.White, 0.55f);

        DrawFlat(h, _glow, pos, grow, color.WithAlpha((0.28f + 0.18f * pulse) * alpha));
        DrawFlat(h, _orbit, pos, grow * 0.85f, color.WithAlpha(0.55f * alpha));

        DrawPetals(h, pos, color, grow, alpha, t, seed, 16, 0.80f, 0.42f, 0.26f, 0f);
        DrawPetals(h, pos, hot, grow, alpha, t, seed + 1.3, 16, 0.78f, 0.26f, 0.17f, 0.5f);
    }

    private void DrawPetals(
        DrawingHandleWorld h, Vector2 pos, Color color, float grow, float alpha,
        double t, double seed, int count, float baseFrac, float heightFrac, float widthFrac, float stepOffset)
    {
        var rotation = t * 0.12 + seed * 0.1;

        for (var i = 0; i < count; i++)
        {
            var ang = (i + stepOffset) * Math.PI * 2 / count + rotation;

            var flick = 0.78 + 0.22 * Math.Sin(t * 3.1 + i * 1.7 + seed) + 0.08 * Math.Sin(t * 7.3 + i * 2.9);
            var height = (float) (R * heightFrac * grow * flick);
            var width = R * widthFrac * grow * (float) (0.92 + 0.08 * Math.Sin(t * 4.1 + i));
            var baseR = R * baseFrac * grow;

            var dir = new Vector2((float) Math.Cos(ang), (float) Math.Sin(ang));
            var center = pos + dir * (baseR + height / 2f);

            var box = Box2.CenteredAround(center, new Vector2(width, height));
            var quad = new Box2Rotated(box, new Angle(ang - Math.PI / 2), center);

            var a = (float) (0.8 + 0.2 * Math.Sin(t * 5.0 + i * 2.3)) * alpha;
            h.DrawTextureRect(_petal, quad, color.WithAlpha(a));
        }
    }

    private void DrawOrbit(DrawingHandleWorld h, Vector2 pos, Color color, float grow, float alpha, double t, double seed)
    {
        var pulse = 0.5f + 0.5f * (float) Math.Sin(t * 2.0 + seed);

        DrawFlat(h, _glow, pos, grow, color.WithAlpha((0.16f + 0.10f * pulse) * alpha));

        for (var i = 0; i < OrbitFracs.Length; i++)
        {
            var dir = i % 2 == 0 ? 1.0 : -1.0;
            var angle = (float) (t * (0.3 + i * 0.2) * dir + seed);
            DrawRotated(h, _orbit, pos, grow * OrbitFracs[i] / RingTex, angle, color.WithAlpha(0.5f * alpha));
        }
    }

    private static void DrawFlat(DrawingHandleWorld handle, Texture tex, Vector2 center, float scale, Color color)
    {
        handle.DrawTextureRect(tex, Box2.CenteredAround(center, new Vector2(R * 2f * scale)), color);
    }

    private static void DrawRotated(
        DrawingHandleWorld handle, Texture tex, Vector2 center, float scale, float angle, Color color)
    {
        var box = Box2.CenteredAround(center, new Vector2(R * 2f * scale));
        handle.DrawTextureRect(tex, new Box2Rotated(box, new Angle(angle), center), color);
    }
}