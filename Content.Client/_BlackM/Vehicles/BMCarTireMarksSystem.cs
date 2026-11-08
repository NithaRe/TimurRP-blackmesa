using System.Numerics;
using Content.Shared._BlackM.Vehicles.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.Vehicles;

// Client-only skid marks, nothing is networked.
public sealed class BMCarTireMarksSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const string MarkPrototype = "BMCarTireMark";
    private const float SampleTime = 0.05f;
    private const float SegmentLength = 0.2f;
    private const float MaxSegmentLength = 1.5f;
    private const float FadeStart = 0.5f;
    private const int MaxMarks = 600;

    private const float HardBrake = 9f;
    private const float LockedRear = 5.5f;
    private const float Slip = 0.8f;
    private const float HeavySlip = 2.2f;
    private const float Burnout = 5f;

    private static readonly Vector2[] Wheels =
    {
        new(0.40f, -0.53f),
        new(-0.40f, -0.53f),
        new(0.40f, 0.53f),
        new(-0.40f, 0.53f),
    };

    private readonly Dictionary<EntityUid, CarState> _cars = new();
    private readonly List<Mark> _marks = new();
    private readonly List<EntityUid> _stale = new();

    public override void Shutdown()
    {
        base.Shutdown();

        foreach (var mark in _marks)
        {
            DeleteMark(mark.Uid);
        }

        _marks.Clear();
        _cars.Clear();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = (float) _timing.RealTime.TotalSeconds;

        _stale.Clear();
        foreach (var uid in _cars.Keys)
        {
            if (TerminatingOrDeleted(uid))
                _stale.Add(uid);
        }

        foreach (var uid in _stale)
        {
            _cars.Remove(uid);
        }

        if (frameTime > 0f)
        {
            var query = EntityQueryEnumerator<BMCarComponent, PhysicsComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var car, out var body, out var xform))
            {
                if (!_cars.TryGetValue(uid, out var state))
                {
                    state = new CarState();
                    _cars[uid] = state;
                }

                UpdateCar(car, body, xform, state, frameTime, now);
            }
        }

        UpdateMarks(now);
    }

    private void UpdateCar(BMCarComponent car, PhysicsComponent body, TransformComponent xform, CarState state, float frameTime, float now)
    {
        if (car.TireMarkLifetime <= 0f || xform.GridUid == null || xform.MapID == MapId.Nullspace)
        {
            state.Reset();
            return;
        }

        var (position, rotation) = _transform.GetWorldPositionRotation(xform);
        var velocity = body.LinearVelocity;
        var forward = rotation.ToWorldVec();
        var speed = Vector2.Dot(velocity, forward);

        state.Timer += frameTime;
        if (state.Timer >= SampleTime)
        {
            var accel = state.Sampled ? (speed - state.LastSpeed) / state.Timer : 0f;
            state.Accel = (state.Accel + accel) * 0.5f;
            state.LastSpeed = speed;
            state.Sampled = true;
            state.Timer = 0f;

            var absSpeed = MathF.Abs(speed);
            var decel = -state.Accel * MathF.Sign(speed);
            var lateral = (velocity - forward * speed).Length();

            var front = absSpeed > 1f && decel > HardBrake;
            var rear = front || absSpeed > 0.8f && decel > LockedRear;

            if (lateral > Slip && velocity.Length() > 1f)
            {
                rear = true;
                front |= lateral > HeavySlip;
            }

            if (speed > 0.2f && speed < 2.5f && state.Accel > Burnout)
                rear = true;

            state.Front = front;
            state.Rear = rear;
        }

        for (var i = 0; i < Wheels.Length; i++)
        {
            if (!(i < 2 ? state.Front : state.Rear))
            {
                state.Last[i] = null;
                continue;
            }

            var wheel = position + rotation.RotateVec(Wheels[i]);
            if (state.Last[i] is not { } last)
            {
                state.Last[i] = wheel;
                continue;
            }

            var delta = wheel - last;
            var length = delta.Length();
            if (length < SegmentLength)
                continue;

            if (length < MaxSegmentLength)
                SpawnMark(xform.MapID, last + delta * 0.5f, delta, length, now, car.TireMarkLifetime);

            state.Last[i] = wheel;
        }
    }

    private void SpawnMark(MapId map, Vector2 center, Vector2 direction, float length, float now, float lifetime)
    {
        var uid = Spawn(MarkPrototype, new MapCoordinates(center, map));
        _transform.SetWorldRotation(Transform(uid), direction.ToWorldAngle());

        if (TryComp<SpriteComponent>(uid, out var sprite))
            _sprite.SetScale((uid, sprite), new Vector2(1f, length + 0.02f));

        _marks.Add(new Mark(uid, now, lifetime));
        if (_marks.Count <= MaxMarks)
            return;

        DeleteMark(_marks[0].Uid);
        _marks.RemoveAt(0);
    }

    private void UpdateMarks(float now)
    {
        for (var i = _marks.Count - 1; i >= 0; i--)
        {
            var mark = _marks[i];
            var age = now - mark.Start;
            if (age >= mark.Lifetime)
            {
                DeleteMark(mark.Uid);
                _marks.RemoveAt(i);
                continue;
            }

            var fadeStart = mark.Lifetime * FadeStart;
            if (age <= fadeStart || !TryComp<SpriteComponent>(mark.Uid, out var sprite))
                continue;

            var alpha = 1f - (age - fadeStart) / (mark.Lifetime - fadeStart);
            _sprite.SetColor((mark.Uid, sprite), sprite.Color.WithAlpha(alpha));
        }
    }

    private void DeleteMark(EntityUid uid)
    {
        if (!TerminatingOrDeleted(uid))
            QueueDel(uid);
    }

    private sealed class CarState
    {
        public readonly Vector2?[] Last = new Vector2?[4];
        public float Timer;
        public float LastSpeed;
        public float Accel;
        public bool Sampled;
        public bool Front;
        public bool Rear;

        public void Reset()
        {
            Array.Clear(Last);
            Timer = 0f;
            Accel = 0f;
            Sampled = false;
            Front = false;
            Rear = false;
        }
    }

    private readonly record struct Mark(EntityUid Uid, float Start, float Lifetime);
}
