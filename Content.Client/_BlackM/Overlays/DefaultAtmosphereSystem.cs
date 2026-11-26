using Content.Shared._BlackM.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Client._BlackM.Overlays;

public sealed partial class DefaultAtmosphereSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private DefaultOverlay? _overlayInstance;

    private float _strength = 1f;
    private float _grain = 0f;
    private float _vignette = 0.5f;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, BlackMCVars.AtmosphereShaderStrength, OnStrengthChanged, true);
        Subs.CVar(_cfg, BlackMCVars.AtmosphereShaderGrain, OnGrainChanged, true);
        Subs.CVar(_cfg, BlackMCVars.AtmosphereShaderVignette, OnVignetteChanged, true);
        _cfg.OnValueChanged(BlackMCVars.DefaultAtmosphereShaderEnabled, OnShaderToggled, invokeImmediately: true);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _cfg.UnsubValueChanged(BlackMCVars.DefaultAtmosphereShaderEnabled, OnShaderToggled);
        RemoveOverlay();
    }

    private void OnStrengthChanged(float value)
    {
        _strength = value;
        ApplySettings();
    }

    private void OnGrainChanged(float value)
    {
        _grain = value;
        ApplySettings();
    }

    private void OnVignetteChanged(float value)
    {
        _vignette = value;
        ApplySettings();
    }

    private void ApplySettings()
    {
        if (_overlayInstance == null)
            return;

        _overlayInstance.Strength = _strength;
        _overlayInstance.Grain = _grain;
        _overlayInstance.Vignette = _vignette;
    }

    private void OnShaderToggled(bool enabled)
    {
        if (enabled)
            AddOverlay();
        else
            RemoveOverlay();
    }

    private void AddOverlay()
    {
        if (_overlayInstance != null)
            return;

        _overlayInstance = new DefaultOverlay();
        ApplySettings();
        _overlay.AddOverlay(_overlayInstance);
    }

    private void RemoveOverlay()
    {
        if (_overlayInstance == null)
            return;

        _overlay.RemoveOverlay(_overlayInstance);
        _overlayInstance = null;
    }
}
