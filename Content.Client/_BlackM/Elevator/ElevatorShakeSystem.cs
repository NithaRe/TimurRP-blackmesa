using System.Numerics;
using Content.Shared._BlackM.Elevator;
using Content.Shared.Camera;
using Content.Shared.CCVar;
using Content.Shared.Movement.Systems;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Random;

namespace Content.Client._BlackM.Elevator;

[RegisterComponent]
public sealed partial class BlackMElevatorShakeComponent : Component
{
    public Vector2 Offset;
}

public sealed class ElevatorShakeSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedContentEyeSystem _eye = default!;

    private const float RumbleAmp = 0.03f;
    private const float SwayAmp = 0.025f;
    private const float JoltScale = 0.22f;

    private const float JoltFreq = 7.5f;
    private const float JoltDecay = 4.2f;
    private const float JoltLife = 1.4f;

    private const float RampUp = 0.7f;
    private const float RampDown = 1.2f;
    private const float SyncTimeout = 2.5f;

    private sealed class Jolt
    {
        public Vector2 Dir;
        public float Amp;
        public float Age;
        public float Freq;
        public float Phase;
    }

    private readonly List<Jolt> _jolts = new();
    private float _time;
    private float _rumble;
    private float _rumbleTarget;
    private float _boost;
    private float _lastSync = float.MinValue;
    private float _swayPhaseX, _swayPhaseY;
    private int _seed;
    private EntityUid? _shaken;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<BlackMElevatorRideEvent>(OnRide);
        SubscribeNetworkEvent<BlackMElevatorJoltEvent>(OnJolt);
        SubscribeLocalEvent<BlackMElevatorShakeComponent, GetEyeOffsetEvent>(OnGetEyeOffset);

        _swayPhaseX = _random.NextFloat(MathF.Tau);
        _swayPhaseY = _random.NextFloat(MathF.Tau);
        _seed = _random.Next(1000);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        StopShake();
    }

    private void OnRide(BlackMElevatorRideEvent ev)
    {
        _lastSync = _time;
        _rumbleTarget = ev.Active ? Math.Clamp(ev.Intensity, 0f, 2f) : 0f;
    }

    private void OnJolt(BlackMElevatorJoltEvent ev)
    {
        var dir = new Vector2(_random.NextFloat(-0.35f, 0.35f), _random.Prob(0.7f) ? -1f : 1f);
        dir = Vector2.Normalize(dir);

        _jolts.Add(new Jolt
        {
            Dir = dir,
            Amp = ev.Strength * JoltScale,
            Freq = JoltFreq * _random.NextFloat(0.85f, 1.15f),
            Phase = _random.NextFloat(-0.2f, 0.2f),
        });

        _boost = MathF.Max(_boost, ev.Strength);
    }

    private void OnGetEyeOffset(Entity<BlackMElevatorShakeComponent> ent, ref GetEyeOffsetEvent args)
    {
        args.Offset += ent.Comp.Offset;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        _time += frameTime;

        if (_rumbleTarget > 0f && _time - _lastSync > SyncTimeout)
            _rumbleTarget = 0f;

        var rate = _rumbleTarget > _rumble ? RampUp : RampDown;
        _rumble += Math.Clamp(_rumbleTarget - _rumble, -rate * frameTime, rate * frameTime);
        _boost = MathF.Max(0f, _boost - frameTime * 1.6f);

        for (var i = _jolts.Count - 1; i >= 0; i--)
        {
            _jolts[i].Age += frameTime;
            if (_jolts[i].Age >= JoltLife)
                _jolts.RemoveAt(i);
        }

        var local = _player.LocalEntity;
        var active = _rumble > 0.001f || _jolts.Count > 0 || _boost > 0.001f;

        if (!active || local == null || TerminatingOrDeleted(local.Value))
        {
            StopShake();
            return;
        }

        if (_shaken != local)
        {
            StopShake();
            _shaken = local;
        }

        var comp = EnsureComp<BlackMElevatorShakeComponent>(local.Value);
        var scale = _cfg.GetCVar(CCVars.ScreenShakeIntensity);
        comp.Offset = ComputeOffset() * scale;

        UpdateEye(local.Value);
    }

    private Vector2 ComputeOffset()
    {
        var t = _time;

        var shiver = _rumble * RumbleAmp + _boost * RumbleAmp * 1.5f;
        var x = Noise(t * 13f, _seed) * shiver * 0.55f;
        var y = Noise(t * 16f, _seed + 1) * shiver;

        var sway = _rumble * SwayAmp;
        x += MathF.Sin(t * 0.85f + _swayPhaseX) * sway * 0.6f;
        y += MathF.Sin(t * 0.55f + _swayPhaseY) * sway * 0.35f;

        var offset = new Vector2(x, y);

        foreach (var j in _jolts)
        {
            var env = MathF.Exp(-j.Age * JoltDecay);
            var attack = Math.Clamp(j.Age / 0.03f, 0f, 1f);
            var osc = MathF.Cos(MathF.Tau * j.Freq * j.Age + j.Phase);
            offset += j.Dir * (j.Amp * env * attack * osc);
        }

        return offset;
    }

    private void StopShake()
    {
        if (_shaken is not { } uid)
            return;

        _shaken = null;

        if (TerminatingOrDeleted(uid))
            return;

        RemComp<BlackMElevatorShakeComponent>(uid);
        UpdateEye(uid);
    }

    private void UpdateEye(EntityUid uid)
    {
        if (TryComp(uid, out EyeComponent? eye))
            _eye.UpdateEyeOffset((uid, eye));
    }

    private static float Noise(float x, int seed)
    {
        var i = (int) MathF.Floor(x);
        var f = x - i;
        var u = f * f * (3f - 2f * f);
        return Hash(i, seed) + (Hash(i + 1, seed) - Hash(i, seed)) * u;
    }

    private static float Hash(int i, int seed)
    {
        unchecked
        {
            var h = (uint) (i * 374761393 + seed * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 8388607.5f - 1f;
        }
    }
}
