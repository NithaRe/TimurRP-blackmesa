using System.Numerics;
using Content.Shared._BlackM.WalkBob;
using Robust.Client.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.WalkBob;

/// <summary>
/// Клиентская система, обрабатывающая визуальный эффект "дыхания" спрайта при ходьбе.
/// Работает по кадрам рендера (FrameUpdate), а не по игровым тикам,
/// т.к. это чисто косметический эффект интерполяции.
/// </summary>
public sealed class WalkBobSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WalkBobComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(EntityUid uid, WalkBobComponent component, ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(uid, out var sprite))
            ResetScale(uid, sprite, component);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<WalkBobComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var bob, out var sprite))
        {
            var speed = bob.VisualSpeedOverride ??
                (TryComp<PhysicsComponent>(uid, out var physics) ? physics.LinearVelocity.Length() : 0f);

            if (speed > bob.MinSpeedThreshold)
            {
                bob.Phase = (bob.Phase + frameTime * bob.Frequency * (speed / 3f)) % MathF.Tau;

                var squash = MathF.Sin(bob.Phase) * bob.Amplitude;
                var scale = new Vector2(1f - squash * 0.5f, 1f + squash);

                if (bob.VisualSpeedOverride != null)
                {
                    var blend = 1f - MathF.Exp(-bob.ReturnLerpSpeed * frameTime);
                    scale = Vector2.Lerp(GetCurrentScale(sprite, bob), scale, blend);
                }
                ApplyScale(uid, sprite, bob, scale);
            }
            else
            {
                var current = GetCurrentScale(sprite, bob);
                // Preserve the phase across brief stops in presentation movement.
                if (bob.VisualSpeedOverride == null || Vector2.DistanceSquared(current, Vector2.One) < 0.000001f)
                    bob.Phase = 0f;
                var blend = bob.VisualSpeedOverride != null
                    ? 1f - MathF.Exp(-bob.ReturnLerpSpeed * frameTime)
                    : Math.Clamp(frameTime * bob.ReturnLerpSpeed, 0f, 1f);
                var lerped = Vector2.Lerp(current, Vector2.One, blend);
                ApplyScale(uid, sprite, bob, lerped);
            }
        }
    }

    private Vector2 GetCurrentScale(SpriteComponent sprite, WalkBobComponent bob)
    {
        if (bob.TargetLayerKey != null && sprite.LayerMapTryGet(bob.TargetLayerKey, out var index))
            return sprite[index].Scale;

        return sprite.Scale;
    }

    private void ApplyScale(EntityUid uid, SpriteComponent sprite, WalkBobComponent bob, Vector2 scale)
    {
        if (bob.TargetLayerKey != null && sprite.LayerMapTryGet(bob.TargetLayerKey, out var index))
        {
            _sprite.LayerSetScale((uid, sprite), index, scale);
            return;
        }

        _sprite.SetScale((uid, sprite), scale);
    }

    private void ResetScale(EntityUid uid, SpriteComponent sprite, WalkBobComponent bob)
    {
        if (bob.TargetLayerKey != null && sprite.LayerMapTryGet(bob.TargetLayerKey, out var index))
        {
            _sprite.LayerSetScale((uid, sprite), index, Vector2.One);
            return;
        }

        _sprite.SetScale((uid, sprite), Vector2.One);
    }
}