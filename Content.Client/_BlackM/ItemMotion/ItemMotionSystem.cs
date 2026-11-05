using System.Numerics;
using Content.Client._BlackM.WalkBob;
using Content.Shared._BlackM.WalkBob;
using Content.Shared.Hands;
using Content.Shared.Item;
using Content.Shared.Throwing;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Animations;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;
using static Robust.Client.Animations.AnimationTrackProperty;

namespace Content.Client._BlackM.ItemMotion;

public sealed class ItemMotionSystem : EntitySystem
{
    [Dependency] private readonly AnimationPlayerSystem _anim = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly WalkBobSystem _walkBob = default!;

    private const string PlaceKey = "blackm-item-place";
    private const string LandKey = "blackm-item-land";

    private const float PlaceLength = 0.2f;
    private const int PlaceSteps = 6;
    private const float HandHeight = 0.12f;
    private const float PlaceArc = 0.08f;
    private const float MaxPlaceDistance = 2f;

    private const float LandHop = 0.07f;
    private const float LandSquash = 0.14f;

    private readonly List<PendingDrop> _pendingDrops = new();
    private readonly Dictionary<EntityUid, Running> _running = new();
    private readonly HashSet<EntityUid> _flying = new();
    private readonly HashSet<EntityUid> _flyingNow = new();
    private readonly List<EntityUid> _toRemove = new();

    private record struct PendingDrop(EntityUid Item, EntityUid User, MapCoordinates From, int Frames);

    private record struct Running(string Key, Vector2 BaseOffset, Vector2 BaseScale, TimeSpan End);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WalkBobComponent, DidUnequipHandEvent>(OnUnequipped);
    }

    private void OnUnequipped(EntityUid uid, WalkBobComponent component, DidUnequipHandEvent args)
    {
        if (!_timing.IsFirstTimePredicted || !HasComp<ItemComponent>(args.Unequipped))
            return;

        foreach (var pending in _pendingDrops)
        {
            if (pending.Item == args.Unequipped)
                return;
        }

        _pendingDrops.Add(new PendingDrop(args.Unequipped, uid, _transform.GetMapCoordinates(uid), 0));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        UpdatePendingDrops();
        UpdateThrown();
        GuardRunning();
    }

    #region Put down

    private void UpdatePendingDrops()
    {
        for (var i = _pendingDrops.Count - 1; i >= 0; i--)
        {
            var pending = _pendingDrops[i];
            if (pending.Frames < 1)
            {
                _pendingDrops[i] = pending with { Frames = pending.Frames + 1 };
                continue;
            }

            _pendingDrops.RemoveAt(i);
            TryPlayPlace(pending);
        }
    }

    private void TryPlayPlace(PendingDrop drop)
    {
        var item = drop.Item;
        if (Deleted(item)
            || _container.IsEntityInContainer(item)
            || HasComp<ThrownItemComponent>(item)
            || !TryComp<SpriteComponent>(item, out var sprite)
            || !sprite.Visible
            || _running.ContainsKey(item))
            return;

        var to = _transform.GetMapCoordinates(item);
        if (to.MapId != drop.From.MapId)
            return;

        var delta = drop.From.Position - to.Position;
        if (delta.Length() > MaxPlaceDistance)
            return;

        _walkBob.Reach(drop.User, drop.From.Position, to.Position);

        var baseOffset = sprite.Offset;
        var baseScale = sprite.Scale;
        var up = ScreenUpWorld();

        var position = Track(nameof(SpriteComponent.Offset));
        var scale = Track(nameof(SpriteComponent.Scale));
        var step = PlaceLength / PlaceSteps;

        for (var s = 0; s <= PlaceSteps; s++)
        {
            var t = (float) s / PlaceSteps;
            var eased = 1f - (1f - t) * (1f - t) * (1f - t); // ease-out
            var arc = 4f * t * (1f - t) * PlaceArc;
            var world = delta * (1f - eased) + up * (HandHeight * (1f - eased) + arc);
            var key = s == 0 ? 0f : step;

            position.KeyFrames.Add(new KeyFrame(baseOffset + ToSpriteSpace(item, sprite, world), key));
            scale.KeyFrames.Add(new KeyFrame(baseScale * (0.75f + 0.25f * eased), key));
        }

        position.KeyFrames.Add(new KeyFrame(baseOffset, 0.05f));
        scale.KeyFrames.Add(new KeyFrame(baseScale * new Vector2(1f + LandSquash * 0.6f, 1f - LandSquash * 0.6f), 0.05f));
        position.KeyFrames.Add(new KeyFrame(baseOffset, 0.06f));
        scale.KeyFrames.Add(new KeyFrame(baseScale, 0.06f));

        Play(item, sprite, PlaceKey, PlaceLength + 0.11f, baseOffset, baseScale, position, scale);
    }

    #endregion

    #region Throw

    private void UpdateThrown()
    {
        _flyingNow.Clear();

        var query = EntityQueryEnumerator<ThrownItemComponent>();
        while (query.MoveNext(out var uid, out var thrown))
        {
            _flyingNow.Add(uid);
            if (_flying.Contains(uid))
                continue;

            // just thrown
            if (thrown.Thrower is { } thrower && Exists(thrower))
            {
                var dir = Vector2.Zero;
                if (TryComp<PhysicsComponent>(uid, out var physics))
                {
                    var parent = Transform(uid).ParentUid;
                    var parentRot = parent.IsValid() ? _transform.GetWorldRotation(parent) : Angle.Zero;
                    dir = parentRot.RotateVec(physics.LinearVelocity);
                }
                if (dir.LengthSquared() < 0.01f)
                    dir = _transform.GetWorldPosition(uid) - _transform.GetWorldPosition(thrower);

                _walkBob.Throw(thrower, dir);
            }
        }

        foreach (var uid in _flying)
        {
            if (!_flyingNow.Contains(uid))
                PlayLand(uid);
        }

        _flying.Clear();
        _flying.UnionWith(_flyingNow);
    }

    private void PlayLand(EntityUid item)
    {
        if (Deleted(item)
            || !HasComp<ItemComponent>(item)
            || _container.IsEntityInContainer(item)
            || !TryComp<SpriteComponent>(item, out var sprite)
            || !sprite.Visible
            || _running.ContainsKey(item))
            return;

        var baseOffset = sprite.Offset;
        var baseScale = sprite.Scale;
        var hop = ToSpriteSpace(item, sprite, ScreenUpWorld() * LandHop);

        var position = Track(nameof(SpriteComponent.Offset));
        var scale = Track(nameof(SpriteComponent.Scale));

        position.KeyFrames.Add(new KeyFrame(baseOffset, 0f));
        scale.KeyFrames.Add(new KeyFrame(baseScale * new Vector2(1f + LandSquash, 1f - LandSquash), 0f));

        position.KeyFrames.Add(new KeyFrame(baseOffset + hop, 0.08f));
        scale.KeyFrames.Add(new KeyFrame(baseScale * new Vector2(0.95f, 1.05f), 0.08f));

        position.KeyFrames.Add(new KeyFrame(baseOffset, 0.08f));
        scale.KeyFrames.Add(new KeyFrame(baseScale * new Vector2(1f + LandSquash * 0.4f, 1f - LandSquash * 0.4f), 0.08f));

        position.KeyFrames.Add(new KeyFrame(baseOffset + hop * 0.25f, 0.06f));
        scale.KeyFrames.Add(new KeyFrame(baseScale, 0.06f));

        position.KeyFrames.Add(new KeyFrame(baseOffset, 0.06f));
        scale.KeyFrames.Add(new KeyFrame(baseScale, 0.06f));

        Play(item, sprite, LandKey, 0.28f, baseOffset, baseScale, position, scale);
    }

    #endregion

    #region Helpers

    private static AnimationTrackComponentProperty Track(string property)
    {
        return new AnimationTrackComponentProperty
        {
            ComponentType = typeof(SpriteComponent),
            Property = property,
            InterpolationMode = AnimationInterpolationMode.Linear,
        };
    }

    private void Play(EntityUid item, SpriteComponent sprite, string key, float length,
        Vector2 baseOffset, Vector2 baseScale, params AnimationTrack[] tracks)
    {
        var player = EnsureComp<AnimationPlayerComponent>(item);
        if (_anim.HasRunningAnimation(item, player, key))
            return;

        var animation = new Animation { Length = TimeSpan.FromSeconds(length) };
        animation.AnimationTracks.AddRange(tracks);

        _anim.Play((item, player), animation, key);
        _running[item] = new Running(key, baseOffset, baseScale, _timing.CurTime + TimeSpan.FromSeconds(length + 0.05f));
    }

    private void GuardRunning()
    {
        _toRemove.Clear();
        foreach (var (uid, run) in _running)
        {
            if (Deleted(uid))
            {
                _toRemove.Add(uid);
                continue;
            }

            if (_container.IsEntityInContainer(uid))
            {
                _anim.Stop(uid, run.Key);
                if (TryComp<SpriteComponent>(uid, out var sprite))
                {
                    _sprite.SetOffset((uid, sprite), run.BaseOffset);
                    _sprite.SetScale((uid, sprite), run.BaseScale);
                }

                _toRemove.Add(uid);
                continue;
            }

            if (_timing.CurTime > run.End)
                _toRemove.Add(uid);
        }

        foreach (var uid in _toRemove)
        {
            _running.Remove(uid);
        }
    }

    private Vector2 ScreenUpWorld()
    {
        return (-_eye.CurrentEye.Rotation).RotateVec(new Vector2(0f, 1f));
    }

    private Vector2 ToSpriteSpace(EntityUid uid, SpriteComponent sprite, Vector2 world)
    {
        var eyeRot = _eye.CurrentEye.Rotation;
        Angle spriteAngle;

        if (sprite.NoRotation)
        {
            spriteAngle = -eyeRot;
        }
        else
        {
            var worldRot = _transform.GetWorldRotation(uid);
            spriteAngle = sprite.SnapCardinals
                ? worldRot - (worldRot + eyeRot).RoundToCardinalAngle()
                : worldRot;
        }

        return (-spriteAngle).RotateVec(world);
    }

    #endregion
}
