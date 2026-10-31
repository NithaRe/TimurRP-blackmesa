using Content.Shared._BlackM.Gib;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Map;

namespace Content.Client._BlackM.Gib;

public sealed class GibBurstSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private const float FlashRange = 4.5f;

    private GibParticleOverlay _overlay = default!;
    private GibFlashOverlay _flash = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new GibParticleOverlay(_cache);
        _flash = new GibFlashOverlay();
        _overlays.AddOverlay(_overlay);
        _overlays.AddOverlay(_flash);
        SubscribeNetworkEvent<GibBurstEvent>(OnBurst);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_overlay);
        _overlays.RemoveOverlay(_flash);
    }

    private void OnBurst(GibBurstEvent ev)
    {
        _overlay.Burst(ev.Map, ev.Position, ev.Strength, ev.Color);

        if (_player.LocalEntity is not { } local)
            return;

        var pos = _xform.GetMapCoordinates(local);
        if (pos.MapId != ev.Map)
            return;

        var dist = (pos.Position - ev.Position).Length();
        if (dist >= FlashRange)
            return;

        _flash.Flash(1f - dist / FlashRange);
    }
}
