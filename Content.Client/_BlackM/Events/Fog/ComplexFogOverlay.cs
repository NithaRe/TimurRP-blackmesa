using System.Numerics;
using Content.Shared._BlackM.Events.Fog;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.Events.Fog;

public sealed class ComplexFogOverlay : Overlay
{
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const string CloudsPath = "/Textures/_BlackM/Effects/Fog/fog_clouds.png";
    private const string WispsPath = "/Textures/_BlackM/Effects/Fog/fog_wisps.png";
    private const string VignettePath = "/Textures/_BlackM/Effects/Fog/fog_vignette.png";

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public float FadeIn;
    public float FadeOut;
    public float Presence;
    public float Density = 0.85f;
    public Color FogColor = Color.FromHex("#c3cec6");
    public MapId MapId = MapId.Nullspace;
    public Vector2 WindOffset;

    private readonly Texture _clouds;
    private readonly Texture _wisps;
    private readonly Texture _vignette;

    public ComplexFogOverlay()
    {
        IoCManager.InjectDependencies(this);

        ZIndex = -1;

        _clouds = _cache.GetResource<TextureResource>(CloudsPath).Texture;
        _wisps = _cache.GetResource<TextureResource>(WispsPath).Texture;
        _vignette = _cache.GetResource<TextureResource>(VignettePath).Texture;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return Presence > 0.001f && args.MapId == MapId && FadeOut < 1f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var aabb = args.WorldAABB.Enlarged(2f);
        var t = (float) (_timing.RealTime.TotalSeconds % 100000.0);
        var q = FadeIn;
        var p = FadeOut;
        var strength = Presence * Math.Clamp(Density, 0f, 1f);

        var haze = ComplexFogMath.Smooth((q - 0.35f) / 0.65f) * (1f - ComplexFogMath.Smooth(p / 0.55f)) * strength;
        var clouds = ComplexFogMath.Smooth((q - 0.12f) / 0.6f) * (1f - ComplexFogMath.Smooth((p - 0.1f) / 0.65f)) * strength;
        var wisps = ComplexFogMath.Smooth(q / 0.45f) * (1f - ComplexFogMath.Smooth((p - 0.3f) / 0.7f)) * strength;

        var breath = MathF.Sin(t * 0.21f);
        var breathA = 0.88f + 0.12f * breath;
        var breathB = 0.88f - 0.12f * breath;

        if (haze > 0.001f)
        {
            handle.DrawRect(aabb, FogColor.WithAlpha(Math.Clamp(0.24f * haze * breathA, 0f, 1f)));
            handle.DrawTextureRect(_vignette, args.WorldBounds, FogColor.WithAlpha(Math.Clamp(0.6f * haze, 0f, 1f)));
        }

        if (clouds > 0.001f)
        {
            DrawTiled(handle, aabb, _clouds, 19f,
                new Vector2(0.30f, 0.11f) * t + WindOffset,
                FogColor.WithAlpha(Math.Clamp(0.52f * clouds * breathA, 0f, 1f)));

            DrawTiled(handle, aabb, _clouds, 11.5f,
                new Vector2(-0.18f, 0.26f) * t + WindOffset * 1.35f + new Vector2(5.3f, 2.7f),
                FogColor.WithAlpha(Math.Clamp(0.40f * clouds * breathB, 0f, 1f)));
        }

        if (wisps > 0.001f)
        {
            DrawTiled(handle, aabb, _wisps, 7.5f,
                new Vector2(0.52f, -0.16f) * t + WindOffset * 1.8f,
                FogColor.WithAlpha(Math.Clamp(0.34f * wisps, 0f, 1f)));
        }
    }

    private static void DrawTiled(DrawingHandleWorld handle, Box2 aabb, Texture texture, float size, Vector2 offset, Color color)
    {
        if (color.A <= 0.001f)
            return;

        offset = new Vector2(offset.X % size, offset.Y % size);

        var startX = MathF.Floor((aabb.Left - offset.X) / size) * size + offset.X;
        var startY = MathF.Floor((aabb.Bottom - offset.Y) / size) * size + offset.Y;
        var dimensions = new Vector2(size, size);

        for (var x = startX; x < aabb.Right; x += size)
        {
            for (var y = startY; y < aabb.Top; y += size)
            {
                handle.DrawTextureRect(texture, Box2.FromDimensions(new Vector2(x, y), dimensions), color);
            }
        }
    }
}
