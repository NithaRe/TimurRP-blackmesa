// SPDX-FileCopyrightText: 2023 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 0x6273 <0x40@keemail.me>
// SPDX-FileCopyrightText: 2024 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Client._BlackM.WalkBob; // BlackM
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.Map;
using Robust.Shared.Spawners;
using static Robust.Client.Animations.AnimationTrackProperty;

namespace Content.Client.Animations;

/// <summary>
///     System that handles animating an entity that a player has picked up.
/// </summary>
public sealed class EntityPickupAnimationSystem : EntitySystem
{
    [Dependency] private readonly AnimationPlayerSystem _animations = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    // BlackM start
    private const float PickupLength = 0.16f;
    private const int PickupSteps = 6;
    private const float ArcHeight = 0.18f;
    private const float EndScale = 0.55f;
    private const float SpinDegrees = 20f;
    // BlackM end

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EntityPickupAnimationComponent, AnimationCompletedEvent>(OnEntityPickupAnimationCompleted);
    }

    private void OnEntityPickupAnimationCompleted(EntityUid uid, EntityPickupAnimationComponent component, AnimationCompletedEvent args)
    {
        Del(uid);
    }

    /// <summary>
    ///     Animates a clone of an entity moving from one point to another before
    ///     being deleted.
    ///     Used when the player picks up an entity.
    /// </summary>
    public void AnimateEntityPickup(EntityUid uid, EntityCoordinates initial, Vector2 final, Angle initialAngle)
    {
        if (Deleted(uid) || !initial.IsValid(EntityManager))
            return;

        var metadata = MetaData(uid);

        if (IsPaused(uid, metadata))
            return;

        var animatableClone = Spawn("clientsideclone", initial);
        EnsureComp<EntityPickupAnimationComponent>(animatableClone);
        var val = metadata.EntityName;
        _metaData.SetEntityName(animatableClone, val);

        if (!TryComp(uid, out SpriteComponent? sprite0))
        {
            Log.Error("Entity ({0}) couldn't be animated for pickup since it doesn't have a {1}!", metadata.EntityName, nameof(SpriteComponent));
            return;
        }

        var sprite = Comp<SpriteComponent>(animatableClone);
        _sprite.CopySprite((uid, sprite0), (animatableClone, sprite));
        _sprite.SetVisible((animatableClone, sprite), true);

        var animations = Comp<AnimationPlayerComponent>(animatableClone);

        var despawn = EnsureComp<TimedDespawnComponent>(animatableClone);
        despawn.Lifetime = 0.25f;
        _transform.SetLocalRotationNoLerp(animatableClone, initialAngle);

        // BlackM start
        var parent = initial.EntityId;
        var worldMatrix = _transform.GetWorldMatrix(parent);
        RaiseLocalEvent(new PickupReachEvent(
            Transform(parent).MapID,
            Vector2.Transform(initial.Position, worldMatrix),
            Vector2.Transform(final, worldMatrix)));

        var baseScale = sprite.Scale;
        var baseRotation = sprite.Rotation;
        var spin = uid.Id % 2 == 0 ? SpinDegrees : -SpinDegrees;
        var stepTime = PickupLength / PickupSteps;

        var position = new AnimationTrackComponentProperty
        {
            ComponentType = typeof(TransformComponent),
            Property = nameof(TransformComponent.LocalPosition),
            InterpolationMode = AnimationInterpolationMode.Linear,
        };
        var scale = new AnimationTrackComponentProperty
        {
            ComponentType = typeof(SpriteComponent),
            Property = nameof(SpriteComponent.Scale),
            InterpolationMode = AnimationInterpolationMode.Linear,
        };
        var rotation = new AnimationTrackComponentProperty
        {
            ComponentType = typeof(SpriteComponent),
            Property = nameof(SpriteComponent.Rotation),
            InterpolationMode = AnimationInterpolationMode.Linear,
        };

        for (var i = 0; i <= PickupSteps; i++)
        {
            var t = (float) i / PickupSteps;
            var eased = 1f - (1f - t) * (1f - t); // ease-out: fast start, soft arrival
            var arc = 4f * t * (1f - t) * ArcHeight; // small hop up on the way to the hand
            var keyTime = i == 0 ? 0f : stepTime;

            position.KeyFrames.Add(new KeyFrame(
                Vector2.Lerp(initial.Position, final, eased) + new Vector2(0f, arc), keyTime));
            scale.KeyFrames.Add(new KeyFrame(baseScale * (1f + (EndScale - 1f) * eased), keyTime));
            rotation.KeyFrames.Add(new KeyFrame(baseRotation + Angle.FromDegrees(spin * eased), keyTime));
        }

        _animations.Play(new Entity<AnimationPlayerComponent>(animatableClone, animations), new Animation
        {
            Length = TimeSpan.FromSeconds(PickupLength),
            AnimationTracks = { position, scale, rotation },
        }, "fancy_pickup_anim");
        // BlackM end
    }
}
