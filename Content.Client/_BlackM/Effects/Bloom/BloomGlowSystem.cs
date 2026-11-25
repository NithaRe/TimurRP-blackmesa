using Content.Shared._BlackM.CCVar;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;
using Content.Shared._BlackM.Effects.Bloom;

namespace Content.Client._BlackM.Effects.Bloom;

public sealed partial class BloomGlowSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private BloomLightLookupSystem _lightLookup = default!;
    [Dependency] private ILightManager _lightManager = default!;
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private TransformSystem _transform = default!;

    private EntityQuery<PointLightComponent> _pointLightQuery;
    private EntityQuery<SpriteComponent> _spriteQuery;
    private BloomGlowOverlay? _overlayInstance;

    public override void Initialize()
    {
        base.Initialize();

        _pointLightQuery = GetEntityQuery<PointLightComponent>();
        _spriteQuery = GetEntityQuery<SpriteComponent>();

        Subs.CVar(_configuration, BlackMCVars.LightBloomStrength, OnStrengthChanged, true);
    }

    public override void Shutdown()
    {
        RemoveOverlay();
        base.Shutdown();
    }

    private void OnStrengthChanged(float strength)
    {
        strength = Math.Clamp(strength, 0f, 1f);

        if (strength <= 0f)
        {
            RemoveOverlay();
            return;
        }

        if (_overlayInstance is null)
        {
            _overlayInstance = new BloomGlowOverlay(
                _lightLookup,
                _lightManager,
                _prototype,
                _sprite,
                _transform,
                _pointLightQuery,
                _spriteQuery,
                (int) DrawDepth.Effects,
                0.8f,
                0.05f,
                strength);

            _overlay.AddOverlay(_overlayInstance);
            return;
        }

        _overlayInstance.GlowStrength = strength;
    }

    private void RemoveOverlay()
    {
        if (_overlayInstance is not { } overlay)
            return;

        if (_overlay.HasOverlay<BloomGlowOverlay>())
            _overlay.RemoveOverlay(overlay);

        overlay.Dispose();
        _overlayInstance = null;
    }
}
