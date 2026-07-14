using Content.Shared._BlackM.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Client._BlackM.Overlays;

/// <summary>
/// global shader
/// </summary>
public sealed class DefaultAtmosphereSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private DefaultOverlay? _overlayInstance;

    public override void Initialize()
    {
        base.Initialize();

        _cfg.OnValueChanged(BlackMCVars.DefaultAtmosphereShaderEnabled, OnShaderToggled, invokeImmediately: true);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _cfg.UnsubValueChanged(BlackMCVars.DefaultAtmosphereShaderEnabled, OnShaderToggled);
        RemoveOverlay();
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
