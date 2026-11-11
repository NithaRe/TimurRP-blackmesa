using System.Numerics;
using Content.Shared._BlackM.EyeTrail;
using Content.Shared.Mobs.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Containers;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.EyeTrail;

public sealed class EyeTrailSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    private const float PixelsPerTile = 32f;

    private const float MaxFrameGap = 0.25f;

    private const float MinJump = 1f;

    internal readonly Dictionary<EntityUid, TrailState> Trails = new();
    private readonly List<EntityUid> _stale = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EyeTrailComponent, ComponentShutdown>(OnShutdown);
        _overlays.AddOverlay(new EyeTrailOverlay(this));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<EyeTrailOverlay>();
        Trails.Clear();
    }

    private void OnShutdown(EntityUid uid, EyeTrailComponent component, ComponentShutdown args)
    {
        Trails.Remove(uid);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = (float) _timing.RealTime.TotalSeconds;
        var eyeRot = _eye.CurrentEye.Rotation;

        var query = EntityQueryEnumerator<EyeTrailComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var trail, out var xform))
        {
            Trails.TryGetValue(uid, out var state);

            if (TryGetWearer(uid, xform, trail, out var wearer, out var sprite))
            {
                if (state == null)
                {
                    state = new TrailState();
                    Trails[uid] = state;
                }

                Emit(state, trail, wearer, sprite, eyeRot, now);
            }
            else if (state != null)
            {
                state.EndAll();
            }

            state?.Prune(now, trail.Lifetime, trail.MaxPoints);
        }

        _stale.Clear();
        foreach (var (uid, state) in Trails)
        {
            if (state.Strands.Count == 0 && !state.HasWearer)
                _stale.Add(uid);
        }

        foreach (var uid in _stale)
            Trails.Remove(uid);
    }

    private bool TryGetWearer(EntityUid uid, TransformComponent xform, EyeTrailComponent trail,
        out EntityUid wearer, out SpriteComponent sprite)
    {
        wearer = default;
        sprite = default!;

        if (!_container.TryGetContainingContainer((uid, xform, null), out var container) || container.ID != trail.Slot)
            return false;

        wearer = container.Owner;
        if (_container.IsEntityOrParentInContainer(wearer) || !_mobState.IsAlive(wearer))
            return false;

        if (!TryComp(wearer, out SpriteComponent? s) || !s.Visible)
            return false;

        sprite = s;
        return true;
    }

    private void Emit(TrailState state, EyeTrailComponent trail, EntityUid wearer, SpriteComponent sprite, Angle eyeRot, float now)
    {
        var xform = Transform(wearer);
        var speed = TryComp<PhysicsComponent>(wearer, out var physics) ? physics.LinearVelocity.Length() : 0f;
        var moving = speed >= trail.MinSpeed;
        var dir = ScreenFacing(xform, sprite, eyeRot);
        var parent = xform.ParentUid;

        if (state.LastUpdate > 0f && now - state.LastUpdate > MaxFrameGap)
            state.EndAll();

        state.LastUpdate = now;

        if (!moving || state.Wearer != wearer || state.Parent != parent || state.Dir != dir)
            state.EndAll();

        state.Wearer = wearer;
        state.Parent = parent;
        state.Dir = dir;
        state.HasWearer = true;

        if (!moving || !parent.IsValid() || !trail.Eyes.TryGetValue(dir, out var eyes) || eyes.Count == 0)
            return;

        var inv = _transform.GetInvWorldMatrix(parent);

        if (HasJumped(state, xform, sprite, inv, eyeRot, speed, now))
        {
            state.EndAll();
            state.HasWearer = true;
        }

        if (state.ActiveCount() == 0)
        {
            foreach (var eye in eyes)
                state.Strands.Add(new TrailStrand { Pixel = eye, Parent = parent });
        }

        foreach (var strand in state.Strands)
        {
            if (!strand.Active)
                continue;

            var local = Vector2.Transform(EyeWorldPosition(xform, sprite, strand.Pixel, eyeRot), inv);
            if (strand.Points.Count == 0 || (strand.Points[^1].Local - local).Length() >= trail.SampleDistance)
                strand.Points.Add(new TrailPoint(local, now));
        }
    }

    private bool HasJumped(TrailState state, TransformComponent xform, SpriteComponent sprite, Matrix3x2 inv,
        Angle eyeRot, float speed, float now)
    {
        foreach (var strand in state.Strands)
        {
            if (!strand.Active || strand.Points.Count == 0)
                continue;

            var last = strand.Points[^1];
            var local = Vector2.Transform(EyeWorldPosition(xform, sprite, strand.Pixel, eyeRot), inv);
            var allowed = MinJump + speed * (now - last.Time) * 2f;
            return (last.Local - local).Length() > allowed;
        }

        return false;
    }

    public Direction ScreenFacing(TransformComponent xform, SpriteComponent sprite, Angle eyeRot)
    {
        if (sprite.EnableDirectionOverride)
            return sprite.DirectionOverride;

        return (_transform.GetWorldRotation(xform) + eyeRot).Reduced().FlipPositive().GetCardinalDir();
    }

    // Same transform chain as the sprite renderer: scale -> rotate -> offset, then upright to the camera.
    public Vector2 EyeWorldPosition(TransformComponent xform, SpriteComponent sprite, Vector2 pixel, Angle eyeRot)
    {
        var local = new Vector2((pixel.X + 0.5f) / PixelsPerTile - 0.5f, 0.5f - (pixel.Y + 0.5f) / PixelsPerTile);
        local = sprite.Rotation.RotateVec(local * sprite.Scale) + sprite.Offset;
        return _transform.GetWorldPosition(xform) + (-eyeRot).RotateVec(local);
    }
}

internal record struct TrailPoint(Vector2 Local, float Time);

internal sealed class TrailStrand
{
    public bool Active = true;
    public Vector2 Pixel;

    public EntityUid Parent;
    public readonly List<TrailPoint> Points = new();
}

internal sealed class TrailState
{
    public EntityUid Wearer;
    public EntityUid Parent;
    public Direction Dir = Direction.Invalid;
    public bool HasWearer;
    public float LastUpdate;
    public readonly List<TrailStrand> Strands = new();

    public int ActiveCount()
    {
        var n = 0;
        foreach (var s in Strands)
        {
            if (s.Active)
                n++;
        }

        return n;
    }

    public void EndAll()
    {
        foreach (var s in Strands)
            s.Active = false;

        HasWearer = false;
    }

    public void Prune(float now, float lifetime, int maxPoints)
    {
        for (var i = Strands.Count - 1; i >= 0; i--)
        {
            var points = Strands[i].Points;
            var old = 0;
            while (old < points.Count && now - points[old].Time > lifetime)
                old++;

            old = Math.Max(old, points.Count - maxPoints);
            if (old > 0)
                points.RemoveRange(0, old);

            if (!Strands[i].Active && points.Count == 0)
                Strands.RemoveAt(i);
        }
    }
}
