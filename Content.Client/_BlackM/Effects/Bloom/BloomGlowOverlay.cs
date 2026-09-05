using System.Numerics;
using Content.Shared._BlackM.Effects.Bloom;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.ComponentTrees;
using Robust.Shared.Enums;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._BlackM.Effects.Bloom;

public sealed class BloomGlowOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> GlowShader = "BlackMLightGlow";

    private readonly BloomLightLookupSystem _lightLookup;
    private readonly ILightManager _lightManager;
    private readonly Dictionary<GlowMaskKey, GlowMaskData> _maskCache = [];
    private readonly EntityQuery<PointLightComponent> _pointLightQuery;
    private readonly EntityQuery<SpriteComponent> _spriteQuery;
    private readonly ShaderInstance _shader;
    private readonly SpriteSystem _sprite;
    private readonly TransformSystem _transform;
    private readonly float _baseHaze;
    private readonly float _hazeDivisor;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;
    public override bool RequestScreenTexture => true;

    private readonly List<GlowLightEntry> _visibleLights = [];

    public float GlowStrength;

    public BloomGlowOverlay(
        BloomLightLookupSystem lightLookup,
        ILightManager lightManager,
        IPrototypeManager prototypeManager,
        SpriteSystem spriteSystem,
        TransformSystem transform,
        EntityQuery<PointLightComponent> pointLightQuery,
        EntityQuery<SpriteComponent> spriteQuery,
        int zIndex,
        float baseHaze,
        float hazeDivisor,
        float strength)
    {
        _lightLookup = lightLookup;
        _lightManager = lightManager;
        _shader = prototypeManager.Index(GlowShader).InstanceUnique();
        _sprite = spriteSystem;
        _transform = transform;
        _pointLightQuery = pointLightQuery;
        _spriteQuery = spriteQuery;
        _baseHaze = baseHaze;
        _hazeDivisor = hazeDivisor;
        GlowStrength = strength;
        ZIndex = zIndex;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (GlowStrength <= 0f || !_lightManager.Enabled)
            return false;

        _visibleLights.Clear();

        var visibleArea = args.WorldAABB.Enlarged(1f);
        var queryState = new GlowLightQueryState(
            _visibleLights,
            _maskCache,
            _pointLightQuery,
            _spriteQuery,
            _sprite,
            _transform);

        _lightLookup.QueryAabb(ref queryState, CollectGlowLight, args.MapId, visibleArea);

        return _visibleLights.Count > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var handle = args.WorldHandle;
        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("base_haze", _baseHaze);
        _shader.SetParameter("haze_divisor", _hazeDivisor / GlowStrength);
        handle.UseShader(_shader);

        foreach (var light in _visibleLights)
        {
            handle.SetTransform(light.WorldMatrix);
            handle.DrawTexture(light.MaskTexture, light.MaskOffset, light.Color);
        }

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }

    protected override void DisposeBehavior()
    {
        _shader.Dispose();
        base.DisposeBehavior();
    }

    private static bool CollectGlowLight(
        ref GlowLightQueryState queryState,
        in ComponentTreeEntry<BloomLightMarkerComponent> entry)
    {
        if (!queryState.PointLightQuery.TryComp(entry.Uid, out var pointLight))
            return true;

        if (!pointLight.Enabled)
            return true;

        if (queryState.SpriteQuery.TryComp(entry.Uid, out var sprite) && !sprite.Visible)
            return true;

        var marker = entry.Component;
        var transform = entry.Transform;
        var (_, _, worldMatrix) = queryState.Transform.GetWorldPositionRotationMatrix(transform);

        var maskKey = new GlowMaskKey(marker.GlowMask, marker.GlowOffset);
        if (!queryState.MaskCache.TryGetValue(maskKey, out var mask))
        {
            var texture = queryState.Sprite.Frame0(marker.GlowMask);
            mask = new GlowMaskData(
                texture,
                marker.GlowOffset - new Vector2(texture.Width, texture.Height) / (2f * EyeManager.PixelsPerMeter));
            queryState.MaskCache.Add(maskKey, mask);
        }

        queryState.VisibleLights.Add(new GlowLightEntry(
            worldMatrix,
            mask.Texture,
            mask.Offset,
            pointLight.Color * marker.GlowTint));

        return true;
    }

    private readonly record struct GlowLightQueryState(
        List<GlowLightEntry> VisibleLights,
        Dictionary<GlowMaskKey, GlowMaskData> MaskCache,
        EntityQuery<PointLightComponent> PointLightQuery,
        EntityQuery<SpriteComponent> SpriteQuery,
        SpriteSystem Sprite,
        TransformSystem Transform);

    private readonly record struct GlowMaskKey(SpriteSpecifier Sprite, Vector2 Offset);

    private readonly record struct GlowMaskData(Texture Texture, Vector2 Offset);

    private readonly record struct GlowLightEntry(
        Matrix3x2 WorldMatrix,
        Texture MaskTexture,
        Vector2 MaskOffset,
        Color Color);
}
