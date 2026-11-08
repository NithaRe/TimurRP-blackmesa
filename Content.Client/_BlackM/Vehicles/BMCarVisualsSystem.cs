using System.Numerics;
using Content.Shared._BlackM.Vehicles;
using Content.Shared._BlackM.Vehicles.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Physics.Components;

namespace Content.Client._BlackM.Vehicles;

public sealed class BMCarVisualsSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const int TireFrames = 3;
    private const float SampleTime = 0.05f;

    private static readonly BMCarVisualLayers[] Wheels =
    {
        BMCarVisualLayers.WheelFrontLeft,
        BMCarVisualLayers.WheelFrontRight,
        BMCarVisualLayers.WheelRearLeft,
        BMCarVisualLayers.WheelRearRight,
    };

    private readonly Dictionary<EntityUid, CarVisualState> _states = new();
    private readonly List<EntityUid> _stale = new();

    private sealed class CarVisualState
    {
        public bool Initialized;
        public Angle SampleRotation;
        public float SampleSpeed;
        public float SampleTimer;
        public float TargetWheel;
        public float Wheel;
        public float Roll;
        public int TireFrame = -1;
        public float BrakeHold;
        public bool? Head;
        public bool? Tail;
        public bool? Brake;
        public bool? Bar;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _states.Clear();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (frameTime <= 0f)
            return;

        // ComponentShutdown is owned by BMCarEngineSoundSystem.
        _stale.Clear();
        foreach (var uid in _states.Keys)
        {
            if (TerminatingOrDeleted(uid) || !HasComp<BMCarComponent>(uid))
                _stale.Add(uid);
        }

        foreach (var uid in _stale)
        {
            _states.Remove(uid);
        }

        var query = EntityQueryEnumerator<BMCarComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var car, out var sprite, out var xform))
        {
            if (!_states.TryGetValue(uid, out var state))
            {
                state = new CarVisualState();
                _states[uid] = state;
            }

            var rotation = _transform.GetWorldRotation(xform);
            var velocity = TryComp<PhysicsComponent>(uid, out var body) ? body.LinearVelocity : Vector2.Zero;
            var speed = Vector2.Dot(velocity, rotation.ToWorldVec());

            if (!state.Initialized)
            {
                state.Initialized = true;
                state.SampleRotation = rotation;
                state.SampleSpeed = speed;
            }

            state.SampleTimer += frameTime;
            var braking = false;
            if (state.SampleTimer >= SampleTime)
            {
                var dt = state.SampleTimer;
                var yaw = (float) (Angle.ShortestDistance(state.SampleRotation, rotation).Theta / dt);
                var turnRate = Math.Max(car.TurnRate, 0.1f);
                var direction = speed < -0.05f ? -1f : 1f;
                state.TargetWheel = Math.Clamp(yaw / turnRate, -1f, 1f) * direction
                    * MathF.PI / 180f * car.MaxWheelAngle;

                var decel = (Math.Abs(speed) - Math.Abs(state.SampleSpeed)) / dt;
                braking = decel < -2.5f && Math.Abs(speed) > 0.4f;

                state.SampleRotation = rotation;
                state.SampleSpeed = speed;
                state.SampleTimer = 0f;
            }

            if (braking)
                state.BrakeHold = 0.35f;
            else
                state.BrakeHold = Math.Max(0f, state.BrakeHold - frameTime);

            state.Wheel += (state.TargetWheel - state.Wheel) * Math.Min(1f, frameTime * 12f);
            state.Roll += Math.Clamp(speed * 10f, -24f, 24f) * frameTime;
            var frame = ((int) MathF.Floor(state.Roll) % TireFrames + TireFrames) % TireFrames;

            for (var i = 0; i < Wheels.Length; i++)
            {
                if (!_sprite.LayerMapTryGet((uid, sprite), Wheels[i], out var layer, false))
                    continue;

                if (frame != state.TireFrame)
                    _sprite.LayerSetRsiState((uid, sprite), layer, $"tire_{frame}");

                if (i < 2)
                    _sprite.LayerSetRotation((uid, sprite), layer, new Angle(state.Wheel));
            }

            state.TireFrame = frame;

            var engineOn = car.Engine != BMCarEngineState.Off;
            SetLight(uid, sprite, BMCarVisualLayers.Headlights, car.Headlights, ref state.Head);
            SetLight(uid, sprite, BMCarVisualLayers.Taillights, engineOn || car.Headlights, ref state.Tail);
            SetLight(uid, sprite, BMCarVisualLayers.Brakelights, engineOn && state.BrakeHold > 0f, ref state.Brake);
            SetLight(uid, sprite, BMCarVisualLayers.Lightbar, car.Siren, ref state.Bar);
        }
    }

    private void SetLight(EntityUid uid, SpriteComponent sprite, BMCarVisualLayers key, bool on, ref bool? last)
    {
        if (last == on)
            return;

        if (!_sprite.LayerMapTryGet((uid, sprite), key, out var layer, false))
            return;

        _sprite.LayerSetVisible((uid, sprite), layer, on);
        last = on;
    }
}
