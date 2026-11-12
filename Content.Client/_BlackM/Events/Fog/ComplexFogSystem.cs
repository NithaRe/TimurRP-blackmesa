using System.Numerics;
using Content.Shared._BlackM.Events.Fog;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.Events.Fog;

public sealed class ComplexFogSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const float PresenceFadeTime = 2.5f;

    private ComplexFogOverlay _overlay = default!;
    private float _presence;
    private Vector2 _windDirection = new(1f, 0.3f);
    private Vector2 _windOffset;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new ComplexFogOverlay();
        _overlayManager.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlayManager.RemoveOverlay(_overlay);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        ComplexFogComponent? fog = null;
        var mapId = MapId.Nullspace;

        if (_player.LocalEntity is { } local && TryComp(local, out TransformComponent? xform))
        {
            mapId = xform.MapID;

            if (!TryComp(xform.GridUid, out fog))
                TryComp(xform.MapUid, out fog);
        }

        if (fog != null)
        {
            var now = _timing.CurTime;
            _overlay.FadeIn = ComplexFogMath.GetFadeIn(fog, now);
            _overlay.FadeOut = ComplexFogMath.GetFadeOut(fog, now);
            _overlay.Density = fog.Density;
            _overlay.FogColor = fog.Color;
            _overlay.MapId = mapId;

            if (fog.WindDirection.LengthSquared() > 0.0001f)
                _windDirection = Vector2.Normalize(fog.WindDirection);

            _presence = MathF.Min(1f, _presence + frameTime / PresenceFadeTime);
        }
        else
        {
            _presence = MathF.Max(0f, _presence - frameTime / PresenceFadeTime);
        }

        if (_presence <= 0f)
        {
            _windOffset = Vector2.Zero;
        }
        else
        {
            var p = _overlay.FadeOut;
            var speed = 0.15f + 6f * p * p;
            _windOffset += _windDirection * speed * frameTime;
        }

        _overlay.Presence = _presence;
        _overlay.WindOffset = _windOffset;
    }
}
