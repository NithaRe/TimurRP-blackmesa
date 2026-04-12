// Originally authored by mirrorcult for EphemeralSpace (https://github.com/EphemeralSpace/ephemeral-space)
// Ported to this project with modifications under the same license.

using Content.Client._BlackM.Viewcone.Overlays;
using Content.Client.Eye;
using Content.Shared._BlackM.Viewcone;
using Content.Shared.MouseRotator;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Client._BlackM.Viewcone;

/// <summary>
///     Handles adding and removing the viewcone overlays, as well as ferrying data between them.
///     Also handles calculating desired view angle for active viewcones so overlays can use it.
/// </summary>
public sealed class BMViewconeOverlayManagementSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private BMViewconeConeOverlay _coneOverlay = default!;
    private BMViewconeSetAlphaOverlay _setAlphaOverlay = default!;
    private BMViewconeResetAlphaOverlay _resetAlphaOverlay = default!;

    private const float LerpHalfLife = 0.05f;

    [Access(typeof(BMViewconeSetAlphaOverlay), typeof(BMViewconeResetAlphaOverlay))]
    public List<(Entity<SpriteComponent> ent, float baseAlpha)> CachedBaseAlphas = new(128);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BMViewconeComponent, ComponentInit>(OnConeManInit);
        SubscribeLocalEvent<BMViewconeComponent, ComponentShutdown>(OnConeManShutdown);
        SubscribeLocalEvent<BMViewconeComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<BMViewconeComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        _coneOverlay = new();
        _setAlphaOverlay = new();
        _resetAlphaOverlay = new();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var enumerator = AllEntityQuery<LerpingEyeComponent, EyeComponent, BMViewconeComponent, TransformComponent>();
        while (enumerator.MoveNext(out var uid, out _, out var eye, out var viewcone, out var xform))
        {
            var eyeAngle = eye.Rotation;
            var rotation = _xform.GetWorldRotation(xform);
            var playerAngle = rotation;
            var desiredWasNull = viewcone.DesiredViewAngle == null;

            if (HasComp<MouseRotatorComponent>(uid))
            {
                var mousePos = _eye.PixelToMap(_input.MouseScreenPosition);
                if (mousePos.MapId != MapId.Nullspace)
                    playerAngle = (mousePos.Position - _xform.GetMapCoordinates(xform).Position).ToAngle() + Angle.FromDegrees(90);

                viewcone.LastMouseRotationAngle = playerAngle;
            }
            else if (viewcone.LastMouseRotationAngle != 0f)
            {
                if (MathHelper.CloseToPercent(viewcone.LastWorldRotationAngle, playerAngle, .000001d))
                {
                    playerAngle = viewcone.LastMouseRotationAngle;
                }
                else
                {
                    viewcone.LastMouseRotationAngle = 0f;
                }
            }

            viewcone.LastWorldRotationAngle = rotation;
            viewcone.DesiredViewAngle = playerAngle + eyeAngle;

            if (desiredWasNull)
            {
                viewcone.ViewAngle = viewcone.DesiredViewAngle.Value;
                continue;
            }

            // framerate-independent lerp
            // https://twitter.com/FreyaHolmer/status/1757836988495847568
            viewcone.ViewAngle = Angle.Lerp(viewcone.ViewAngle, viewcone.DesiredViewAngle.Value, 1f - MathF.Pow(2f, -(frameTime / LerpHalfLife)));
        }
    }

    private void OnPlayerAttached(Entity<BMViewconeComponent> entity, ref LocalPlayerAttachedEvent args)
    {
        AddOverlays();
    }

    private void OnPlayerDetached(Entity<BMViewconeComponent> entity, ref LocalPlayerDetachedEvent args)
    {
        RemoveOverlays();
    }

    private void OnConeManInit(Entity<BMViewconeComponent> entity, ref ComponentInit args)
    {
        if (_playerManager.LocalSession?.AttachedEntity == entity.Owner)
            AddOverlays();
    }

    private void OnConeManShutdown(Entity<BMViewconeComponent> entity, ref ComponentShutdown args)
    {
        if (_playerManager.LocalSession?.AttachedEntity == entity.Owner)
            RemoveOverlays();
    }

    private void AddOverlays()
    {
        _overlayMan.AddOverlay(_coneOverlay);
        _overlayMan.AddOverlay(_setAlphaOverlay);
        _overlayMan.AddOverlay(_resetAlphaOverlay);
    }

    private void RemoveOverlays()
    {
        _overlayMan.RemoveOverlay(_coneOverlay);
        _overlayMan.RemoveOverlay(_setAlphaOverlay);
        _overlayMan.RemoveOverlay(_resetAlphaOverlay);
    }
}
