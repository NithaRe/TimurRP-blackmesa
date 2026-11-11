using System.Numerics;
using Content.Shared._BlackM.VoidFall;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.Random;

namespace Content.Client._BlackM.VoidFall;

public sealed class BlackMVoidFallVisualsSystem : SharedBlackMVoidFallSystem
{
    [Dependency] private readonly AnimationPlayerSystem _anim = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private const string AnimationKey = "blackm_void_fall";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlackMVoidFallingComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<BlackMVoidFallingComponent, ComponentRemove>(OnRemove);
    }

    private void OnInit(Entity<BlackMVoidFallingComponent> ent, ref ComponentInit args)
    {
        if (TerminatingOrDeleted(ent) || !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        ent.Comp.OriginalScale = sprite.Scale;
        ent.Comp.OriginalOffset = sprite.Offset;
        ent.Comp.OriginalRotation = sprite.Rotation;
        ent.Comp.OriginalColor = sprite.Color;

        var player = EnsureComp<AnimationPlayerComponent>(ent);
        if (_anim.HasRunningAnimation(player, AnimationKey))
            return;

        _anim.Play((ent, player), BuildAnimation(ent.Comp), AnimationKey);
    }

    private void OnRemove(Entity<BlackMVoidFallingComponent> ent, ref ComponentRemove args)
    {
        if (TryComp<AnimationPlayerComponent>(ent, out var player) && _anim.HasRunningAnimation(player, AnimationKey))
            _anim.Stop((ent, player), AnimationKey);

        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _sprite.SetScale((ent, sprite), ent.Comp.OriginalScale);
        _sprite.SetOffset((ent, sprite), ent.Comp.OriginalOffset);
        _sprite.SetRotation((ent, sprite), ent.Comp.OriginalRotation);
        _sprite.SetColor((ent, sprite), ent.Comp.OriginalColor);
    }

    // Keyframe times are deltas from the previous frame.
    private Animation BuildAnimation(BlackMVoidFallingComponent comp)
    {
        var hang = (float) comp.HangTime.TotalSeconds;
        var drop = MathF.Max((float) comp.Duration.TotalSeconds - hang, 0.1f);
        var spin = Angle.FromDegrees(_random.Prob(0.5f) ? 900 : -900);

        var scale = comp.OriginalScale;
        var offset = comp.OriginalOffset;
        var rotation = comp.OriginalRotation;
        var color = comp.OriginalColor;

        return new Animation
        {
            Length = TimeSpan.FromSeconds(hang + drop),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Scale),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(scale, 0f),
                        new AnimationTrackProperty.KeyFrame(scale * new Vector2(0.85f, 1.2f), hang * 0.5f),
                        new AnimationTrackProperty.KeyFrame(scale, hang * 0.5f),
                        new AnimationTrackProperty.KeyFrame(scale * 0.6f, drop * 0.4f),
                        new AnimationTrackProperty.KeyFrame(scale * 0.05f, drop * 0.6f),
                    },
                },
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(offset, 0f),
                        new AnimationTrackProperty.KeyFrame(offset + new Vector2(0f, 0.25f), hang * 0.5f),
                        new AnimationTrackProperty.KeyFrame(offset + new Vector2(0f, 0.2f), hang * 0.5f),
                        new AnimationTrackProperty.KeyFrame(offset + new Vector2(0f, -0.6f), drop),
                    },
                },
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Rotation),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(rotation, 0f),
                        new AnimationTrackProperty.KeyFrame(rotation, hang),
                        new AnimationTrackProperty.KeyFrame(rotation + spin, drop),
                    },
                },
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Color),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(color, 0f),
                        new AnimationTrackProperty.KeyFrame(color, hang + drop * 0.6f),
                        new AnimationTrackProperty.KeyFrame(color.WithAlpha(0f), drop * 0.4f),
                    },
                },
            },
        };
    }
}
