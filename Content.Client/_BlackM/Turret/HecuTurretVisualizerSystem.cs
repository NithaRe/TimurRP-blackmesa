using Content.Shared._BlackM.Turret;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Client._BlackM.Turret;

public sealed class HecuTurretVisualizerSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HecuTurretComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    private void OnHandleState(EntityUid uid, HecuTurretComponent comp, ref AfterAutoHandleStateEvent args)
    {
        comp.VisualRotation ??= comp.Rotation;

        if (!TryComp<SpriteComponent>(uid, out var sprite) ||
            !_sprite.LayerMapTryGet((uid, sprite), HecuTurretLayers.Head, out var layer, false))
        {
            return;
        }

        if (comp.LastVisualState != comp.State)
        {
            comp.LastVisualState = comp.State;
            var rsiState = comp.State switch
            {
                HecuTurretState.Preparing => comp.PrepareStateName,
                HecuTurretState.Armed => comp.ArmedState,
                _ => comp.IdleState,
            };
            _sprite.LayerSetRsiState((uid, sprite), layer, rsiState);
        }

        if (comp.LastShotCount != comp.ShotCount)
        {
            // First state only syncs the counter
            if (comp.LastShotCount != null && comp.State != HecuTurretState.Idle)
            {
                _sprite.LayerSetRsiState((uid, sprite), layer, comp.FireState);
                _sprite.LayerSetAnimationTime((uid, sprite), layer, 0f);
            }

            comp.LastShotCount = comp.ShotCount;
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<HecuTurretComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var sprite, out var xform))
        {
            var current = comp.VisualRotation ?? comp.Rotation;

            if (comp.State != HecuTurretState.Idle &&
                comp.Target is { } target &&
                Exists(target))
            {
                var delta = _transform.GetWorldPosition(target) - _transform.GetWorldPosition(xform);
                if (delta.LengthSquared() > 0.0001f)
                {
                    var goal = delta.ToWorldAngle() - _transform.GetWorldRotation(xform);
                    var step = Angle.FromDegrees(comp.RotationSpeed).Theta * frameTime;
                    current = HecuTurretMath.RotateTowards(current, goal, step);
                }
            }
            else if (comp.State == HecuTurretState.Idle && comp.IdleRotateSpeed > 0f)
            {
                var step = Angle.FromDegrees(comp.IdleRotateSpeed).Theta * frameTime;
                current = HecuTurretMath.Spin(current, step);
            }

            comp.VisualRotation = current;
            _sprite.LayerSetRotation((uid, sprite), HecuTurretLayers.Head, current);
        }
    }
}
