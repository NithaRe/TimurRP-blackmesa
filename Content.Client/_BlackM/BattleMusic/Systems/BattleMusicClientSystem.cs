using Content.Shared._BlackM.BattleMusic;
using Content.Shared._BlackM.CCVar;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Player;

namespace Content.Client._BlackM.BattleMusic;

public sealed class BattleMusicClientSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private static readonly SoundSpecifier BattleMusicPath = new SoundPathSpecifier("/Audio/_BlackM/Battlemusic/battle_music.ogg");

    private const float MaxVolumeDb = 0f;
    private const float SilenceVolumeDb = -40f;
    private const float FadeSpeed = 0.35f;

    private EntityUid? _musicStream;
    private bool _isActive;

    private float _currentLinear;
    private float _targetLinear;

    private bool _enabled = true;
    private float _userVolume = 1f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<BattleMusicStartMessage>(OnBattleStart);
        SubscribeNetworkEvent<BattleMusicStopMessage>(OnBattleStop);

        _cfg.OnValueChanged(BlackMCVars.BattleMusicEnabled, OnEnabledChanged, true);
        _cfg.OnValueChanged(BlackMCVars.BattleMusicVolume, OnVolumeChanged, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _cfg.UnsubValueChanged(BlackMCVars.BattleMusicEnabled, OnEnabledChanged);
        _cfg.UnsubValueChanged(BlackMCVars.BattleMusicVolume, OnVolumeChanged);

        StopMusicImmediate();
    }

    private void OnEnabledChanged(bool enabled)
    {
        _enabled = enabled;

        if (!enabled)
        {
            _isActive = false;
            _targetLinear = 0f;
        }
    }

    private void OnVolumeChanged(float volume)
    {
        _userVolume = volume;
        ApplyVolume();
    }

    private void OnBattleStart(BattleMusicStartMessage msg)
    {
        if (!_enabled)
            return;

        if (_musicStream == null || !EntityManager.EntityExists(_musicStream.Value))
            StartMusic();

        _isActive = true;
        _targetLinear = 1f;
    }

    private void OnBattleStop(BattleMusicStopMessage msg)
    {
        _isActive = false;
        _targetLinear = 0f;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_musicStream == null && !_isActive)
            return;

        var diff = _targetLinear - _currentLinear;
        if (Math.Abs(diff) > 0.001f)
        {
            _currentLinear += Math.Sign(diff) * FadeSpeed * frameTime;
            _currentLinear = Math.Clamp(_currentLinear, 0f, 1f);
            ApplyVolume();
        }
        else
        {
            _currentLinear = _targetLinear;
        }

        if (!_isActive && _currentLinear <= 0.001f && _musicStream != null)
            StopMusicImmediate();
    }

    private void StartMusic()
    {
        _currentLinear = 0f;

        var audioParams = AudioParams.Default
            .WithVolume(SilenceVolumeDb)
            .WithLoop(true);

        _musicStream = _audio.PlayGlobal(
            BattleMusicPath,
            Filter.Local(),
            false,
            audioParams
        )?.Entity;
    }

    private void StopMusicImmediate()
    {
        if (_musicStream != null && EntityManager.EntityExists(_musicStream.Value))
            _audio.Stop(_musicStream.Value);

        _musicStream = null;
        _currentLinear = 0f;
    }

    private void ApplyVolume()
    {
        if (_musicStream == null || !EntityManager.EntityExists(_musicStream.Value))
            return;

        var linear = _currentLinear * _userVolume;

        float db;
        if (linear <= 0.001f)
        {
            db = SilenceVolumeDb;
        }
        else
        {
            db = 20f * MathF.Log10(linear) + MaxVolumeDb;
            db = Math.Max(db, SilenceVolumeDb);
        }

        _audio.SetVolume(_musicStream.Value, db);
    }
}
