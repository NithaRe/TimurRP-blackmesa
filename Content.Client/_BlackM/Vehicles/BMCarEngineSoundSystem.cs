using Content.Shared._BlackM.Vehicles.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;

namespace Content.Client._BlackM.Vehicles;

public sealed class BMCarEngineSoundSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private const float SilentDb = -60f;
    private static readonly float[] LayerPitch = { 1f, 1.28f, 1.6f };

    private readonly Dictionary<EntityUid, EntityUid?[]> _streams = new();
    private readonly List<EntityUid> _toStop = new();
    private readonly Dictionary<EntityUid, EntityUid?> _sirens = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BMCarComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        foreach (var streams in _streams.Values)
        {
            StopStreams(streams);
        }

        _streams.Clear();

        foreach (var siren in _sirens.Values)
        {
            if (siren != null && Exists(siren.Value))
                _audio.Stop(siren);
        }

        _sirens.Clear();
    }

    private void OnShutdown(Entity<BMCarComponent> ent, ref ComponentShutdown args)
    {
        Stop(ent.Owner);
        StopSiren(ent.Owner);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        _toStop.Clear();
        foreach (var uid in _streams.Keys)
        {
            if (TerminatingOrDeleted(uid)
                || !TryComp<BMCarComponent>(uid, out var comp)
                || comp.Engine != BMCarEngineState.Running)
            {
                _toStop.Add(uid);
            }
        }

        foreach (var uid in _toStop)
        {
            Stop(uid);
        }

        _toStop.Clear();
        foreach (var uid in _sirens.Keys)
        {
            if (TerminatingOrDeleted(uid) || !TryComp<BMCarComponent>(uid, out var comp) || !comp.Siren)
                _toStop.Add(uid);
        }

        foreach (var uid in _toStop)
        {
            StopSiren(uid);
        }

        var query = EntityQueryEnumerator<BMCarComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var car, out var body))
        {
            if (car.Siren && !_sirens.ContainsKey(uid))
            {
                var sirenParams = AudioParams.Default
                    .WithLoop(true)
                    .WithVolume(car.SirenVolume)
                    .WithMaxDistance(car.SirenRange);
                _sirens[uid] = _audio.PlayEntity(car.SirenSound, Filter.Local(), uid, false, sirenParams)?.Entity;
            }

            if (car.Engine != BMCarEngineState.Running)
                continue;

            if (!_streams.TryGetValue(uid, out var streams))
            {
                streams = Start(uid, car);
                _streams[uid] = streams;
            }

            var t = Math.Clamp(body.LinearVelocity.Length() / Math.Max(car.MaxSpeed, 0.1f), 0f, 1f);
            SetLayer(streams[0], car, 1f - 0.75f * t);
            SetLayer(streams[1], car, Math.Max(0f, 1f - Math.Abs(t - 0.45f) / 0.4f));
            SetLayer(streams[2], car, Math.Clamp((t - 0.5f) / 0.4f, 0f, 1f));
        }
    }

    private EntityUid?[] Start(EntityUid uid, BMCarComponent car)
    {
        var result = new EntityUid?[LayerPitch.Length];
        for (var i = 0; i < LayerPitch.Length; i++)
        {
            var audioParams = AudioParams.Default
                .WithLoop(true)
                .WithPitchScale(LayerPitch[i])
                .WithVolume(i == 0 ? car.EngineVolume : SilentDb)
                .WithMaxDistance(car.EngineRange);

            var stream = _audio.PlayEntity(car.EngineLoopSound, Filter.Local(), uid, false, audioParams);
            result[i] = stream?.Entity;
        }

        return result;
    }

    private void SetLayer(EntityUid? stream, BMCarComponent car, float gain)
    {
        if (stream == null || !Exists(stream.Value))
            return;

        var volume = gain <= 0.001f ? SilentDb : car.EngineVolume + 20f * MathF.Log10(gain);
        _audio.SetVolume(stream, volume);
    }

    private void Stop(EntityUid uid)
    {
        if (_streams.Remove(uid, out var streams))
            StopStreams(streams);
    }

    private void StopSiren(EntityUid uid)
    {
        if (_sirens.Remove(uid, out var siren) && siren != null && Exists(siren.Value))
            _audio.Stop(siren);
    }

    private void StopStreams(EntityUid?[] streams)
    {
        foreach (var stream in streams)
        {
            if (stream != null && Exists(stream.Value))
                _audio.Stop(stream);
        }
    }
}
