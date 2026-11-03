using System.Linq;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Maths;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.Gib;

public sealed class GibScreenOverlay : Overlay
{
    [Dependency] private readonly IRobustRandom _random = default!;

    private const string Path = "/Textures/_BlackM/Effects/Gib/";

    private const float VignetteLife = 2.2f;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private readonly Texture _vignette;
    private readonly Texture[] _lens;
    private readonly List<LensSplat> _splats = new();

    private float _vigAge = float.MaxValue;
    private float _vigIntensity;
    private Color _vigColor;

    private sealed class LensSplat
    {
        public Texture Tex = default!;
        public Vector2 Pos;
        public float Size;
        public float Age, Life;
        public float Drip;
        public float Intensity;
        public Color Color;
    }

    public GibScreenOverlay(IResourceCache cache)
    {
        IoCManager.InjectDependencies(this);

        var vignette = cache.GetResource<RSIResource>(Path + "vignette.rsi").RSI;
        _vignette = vignette["vignette"].GetFrames(default)[0];

        var lens = cache.GetResource<RSIResource>(Path + "lens.rsi").RSI;
        _lens = Enumerable.Range(1, 5).Select(i => lens[$"lens{i}"].GetFrames(default)[0]).ToArray();
    }

    public void Flash(float intensity, Color blood)
    {
        intensity = Math.Clamp(intensity, 0.15f, 1f);
        var color = new Color(MathF.Min(blood.R * 1.7f, 1f), MathF.Min(blood.G * 1.7f, 1f), MathF.Min(blood.B * 1.7f, 1f), 1f);

        var left = _vigAge < VignetteLife ? _vigIntensity * (1f - _vigAge / VignetteLife) : 0f;
        _vigIntensity = MathF.Max(left, intensity);
        _vigAge = 0f;
        _vigColor = color;

        var count = 1 + (int) (intensity * 3.5f) + _random.Next(2);
        for (var i = 0; i < count; i++)
        {
            Vector2 pos;
            var roll = _random.NextFloat();
            var u = _random.NextFloat(0.08f, 0.92f);
            var v = _random.NextFloat(0.12f, 0.88f);

            if (roll < 0.25f) pos = new Vector2(u, _random.NextFloat(0.04f, 0.22f));
            else if (roll < 0.45f) pos = new Vector2(u, _random.NextFloat(0.78f, 0.96f));
            else if (roll < 0.70f) pos = new Vector2(_random.NextFloat(0.04f, 0.20f), v);
            else if (roll < 0.95f) pos = new Vector2(_random.NextFloat(0.80f, 0.96f), v);
            else pos = new Vector2(u, v);

            _splats.Add(new LensSplat
            {
                Tex = _random.Pick(_lens), Pos = pos,
                Size = _random.NextFloat(0.30f, 0.58f) * (0.65f + 0.55f * intensity),
                Life = _random.NextFloat(2.6f, 4.0f), Drip = _random.NextFloat(0.06f, 0.16f),
                Intensity = intensity, Color = color,
            });
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        var dt = args.DeltaSeconds;

        if (_vigAge < VignetteLife)
            _vigAge += dt;

        for (var i = _splats.Count - 1; i >= 0; i--)
        {
            var s = _splats[i];
            s.Age += dt;
            if (s.Age >= s.Life)
                _splats.RemoveAt(i);
        }
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var vp = args.ViewportBounds;
        var w = (float) vp.Width;
        var h = (float) vp.Height;
        var handle = args.ScreenHandle;

        handle.SetTransform(Matrix3x2.Identity);

        if (_vigAge < VignetteLife)
        {
            var t = _vigAge / VignetteLife;
            var alpha = _vigIntensity * 0.50f * Smooth(0f, 0.04f, t) * (1f - Smooth(0.25f, 1f, t));

            if (alpha > 0.01f)
            {
                var k = MathF.Ceiling(MathF.Max(w / 256f, h / 144f));
                var hw = 128f * k;
                var hh = 72f * k;
                var cx = MathF.Round(vp.Left + w * 0.5f);
                var cy = MathF.Round(vp.Top + h * 0.5f);

                handle.DrawTextureRect(_vignette, new UIBox2(cx - hw, cy - hh, cx + hw, cy + hh),
                    new Color(_vigColor.R, _vigColor.G, _vigColor.B, Math.Clamp(alpha, 0f, 1f)));
            }
        }

        foreach (var s in _splats)
        {
            var t = s.Age / s.Life;
            var alpha = s.Intensity * 0.95f * Smooth(0f, 0.03f, t) * (1f - Smooth(0.55f, 1f, t));
            if (alpha <= 0.01f)
                continue;

            var k = MathF.Max(2f, MathF.Round(s.Size * h / 128f));
            var size = 128f * k;
            var step = 4f * k;
            var dripPx = MathF.Floor(s.Drip * MathF.Pow(t, 1.2f) * h / step) * step;
            var cx = MathF.Round(vp.Left + s.Pos.X * w);
            var cy = MathF.Round(vp.Top + s.Pos.Y * h + dripPx);
            var half = size * 0.5f;

            handle.DrawTextureRect(s.Tex, new UIBox2(cx - half, cy - half, cx + half, cy + half),
                new Color(s.Color.R * 0.85f, s.Color.G * 0.85f, s.Color.B * 0.85f, Math.Clamp(alpha, 0f, 1f)));
        }
    }

    private static float Smooth(float a, float b, float x)
    {
        var t = Math.Clamp((x - a) / (b - a), 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
