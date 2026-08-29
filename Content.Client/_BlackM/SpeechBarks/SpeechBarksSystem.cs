using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared._BlackM.SpeechBarks;
using Content.Shared._BlackM.CCVar;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Client.Player;
using Robust.Client.Audio;
using Robust.Shared.Timing;
using Robust.Shared.Map;

namespace Content.Client._BlackM.SpeechBarks;

public sealed class SpeechBarksSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const float MinimalVolume = -10f;
    private const float WhisperFade = 4f;
    private const float NormalHearingDistance = 10f;
    private const float WhisperHearingDistance = 5f;

    private float _volume;

    private readonly List<ActiveBark> _activeBarks = new();

    public override void Initialize()
    {
        base.Initialize();

        _cfg.OnValueChanged(BlackMCVars.BarksVolume, v => _volume = v, true);

        SubscribeNetworkEvent<PlaySpeechBarksEvent>(OnPlaySpeechBarks);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _cfg.UnsubValueChanged(BlackMCVars.BarksVolume, v => _volume = v);
    }

    private float GetVolume(string message, bool isWhisper)
    {
        var volume = isWhisper ? _volume - WhisperFade : _volume;

        if (message.EndsWith("!"))
            volume += 1.5f;

        return MinimalVolume + SharedAudioSystem.GainToVolume(volume);
    }

    private float GetHearingDistance(bool isWhisper)
    {
        return isWhisper ? WhisperHearingDistance : NormalHearingDistance;
    }

    private void OnPlaySpeechBarks(PlaySpeechBarksEvent ev)
    {
        if (!_cfg.GetCVar(BlackMCVars.ReplaceTTSWithBarks))
            return;

        if (ev.Message == null)
            return;

        if (!TryGetEntity(ev.Source, out var source) || Transform(source.Value).MapID == MapId.Nullspace)
            return;

        _activeBarks.Add(new ActiveBark(
            source,
            ev.SoundSpecifier,
            GetVolume(ev.Message, ev.IsWhisper),
            ev.Pitch,
            GetHearingDistance(ev.IsWhisper),
            (ev.LowVariation, ev.HighVariation),
            ev.Message.Length / 3 + 1));
    }

    public void PlayDataPreview(string protoId, float pitch, float lowVariation, float highVariation)
    {
        if (!_proto.TryIndex<BlackMBarkPrototype>(protoId, out var proto))
            return;

        _activeBarks.Add(new ActiveBark(
            null,
            proto.Sound,
            GetVolume("Test message", false),
            pitch,
            GetHearingDistance(false),
            (lowVariation, highVariation),
            9));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_player.LocalSession == null)
            return;

        for (var i = _activeBarks.Count - 1; i >= 0; i--)
        {
            var bark = _activeBarks[i];

            if (bark.NextSound > _timing.CurTime)
                continue;

            if (bark.SyllablesPlayed >= bark.SyllableCount)
            {
                _activeBarks.RemoveAt(i);
                continue;
            }

            var audioParams = AudioParams.Default
                .WithPitchScale(_random.NextFloat(bark.Pitch - 0.1f, bark.Pitch + 0.1f))
                .WithVolume(bark.Volume)
                .WithMaxDistance(bark.Distance);

            bark.SyllablesPlayed++;
            bark.NextSound = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(bark.DelayVariation.Item1, bark.DelayVariation.Item2));

            if (bark.Source == null)
            {
                if (bark.HadSource)
                    _activeBarks.RemoveAt(i);
                else
                    _audio.PlayGlobal(_audio.ResolveSound(bark.Sound), _player.LocalSession, audioParams);

                continue;
            }

            if (_player.LocalEntity is { Valid: true } localPlayer)
            {
                if (bark.Source == localPlayer)
                    _audio.PlayGlobal(_audio.ResolveSound(bark.Sound), localPlayer, audioParams);
                else
                    _audio.PlayEntity(_audio.ResolveSound(bark.Sound), _player.LocalSession, bark.Source.Value, audioParams);
            }
            else
            {
                _activeBarks.RemoveAt(i);
            }
        }
    }

    private sealed class ActiveBark
    {
        public readonly EntityUid? Source;
        public readonly bool HadSource;
        public readonly SoundSpecifier Sound;
        public readonly float Volume;
        public readonly float Pitch;
        public readonly float Distance;
        public readonly (float, float) DelayVariation;
        public readonly int SyllableCount;

        public TimeSpan NextSound = TimeSpan.Zero;
        public int SyllablesPlayed;

        public ActiveBark(EntityUid? source, SoundSpecifier sound, float volume, float pitch, float distance, (float, float) delayVariation, int syllableCount)
        {
            Source = source;
            HadSource = source.HasValue;
            Sound = sound;
            Volume = volume;
            Pitch = pitch;
            Distance = distance;
            DelayVariation = delayVariation;
            SyllableCount = syllableCount;
        }
    }
}
