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

        var query = EntityQueryEnumerator<WalkBobComponent, SpriteComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var bob, out var sprite, out var physics))
        {
            var speed = physics.LinearVelocity.Length();

            if (speed > bob.MinSpeedThreshold)
            {
                bob.Phase += frameTime * bob.Frequency * (speed / 3f);

                var squash = MathF.Sin(bob.Phase) * bob.Amplitude;
                var scale = new Vector2(1f - squash * 0.5f, 1f + squash);

                ApplyScale(uid, sprite, bob, scale);
            }
            else
            {
                bob.Phase = 0f;
                var current = GetCurrentScale(sprite, bob);
                var lerped = Vector2.Lerp(current, Vector2.One, frameTime * bob.ReturnLerpSpeed);
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