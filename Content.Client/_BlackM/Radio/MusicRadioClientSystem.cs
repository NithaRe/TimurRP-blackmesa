using Content.Client.Gameplay;
using Content.Shared._BlackM.CCVar;
using Content.Shared._BlackM.Radio;
using Robust.Client.Audio;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Client._BlackM.Radio;

public sealed class MusicRadioClientSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IStateManager _state = default!;

    private float _volumeAdjustment;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(AudioSystem));
        SubscribeLocalEvent<MusicRadioComponent, AfterAutoHandleStateEvent>(OnAfterState);
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnLocalPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnLocalPlayerDetached);
        Subs.CVar(_cfg, BlackMCVars.MusicRadioVolume, OnVolumeChanged, true);

        _state.OnStateChanged += OnStateChanged;
    }

    public override void Shutdown()
    {
        _state.OnStateChanged -= OnStateChanged;
        base.Shutdown();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (CanHearRadio)
            ApplyVolumeToRadios();
        else
            MuteRadios();
    }

    private bool CanHearRadio => _state.CurrentState is GameplayState
                                 && _player.LocalEntity is { } player
                                 && Exists(player)
                                 && !Terminating(player);

    private void OnLocalPlayerAttached(LocalPlayerAttachedEvent args)
    {
        if (CanHearRadio)
            ApplyVolumeToRadios();
    }

    private void OnLocalPlayerDetached(LocalPlayerDetachedEvent args)
    {
        MuteRadios();
    }

    private void OnStateChanged(StateChangedEventArgs args)
    {
        if (!CanHearRadio)
            MuteRadios();
    }

    private void ApplyVolumeToRadios()
    {
        var query = EntityQueryEnumerator<MusicRadioComponent>();
        while (query.MoveNext(out EntityUid _, out var radio))
        {
            ApplyVolume(radio);
        }
    }

    private void OnAfterState(EntityUid uid, MusicRadioComponent radio, ref AfterAutoHandleStateEvent args)
    {
        if (CanHearRadio)
            ApplyVolume(radio);
        else
            MuteRadio(radio);
    }

    private void OnVolumeChanged(float volume)
    {
        _volumeAdjustment = SharedAudioSystem.GainToVolume(volume);

        if (CanHearRadio)
            ApplyVolumeToRadios();
    }

    private void ApplyVolume(MusicRadioComponent radio)
    {
        if (radio.AudioStream is not { } stream || !TryComp<AudioComponent>(stream, out var audio))
            return;

        var volume = radio.Volume + _volumeAdjustment;
        _audio.SetVolume(stream, volume, audio);
    }

    private void MuteRadios()
    {
        var query = EntityQueryEnumerator<MusicRadioComponent>();
        while (query.MoveNext(out EntityUid _, out var radio))
        {
            MuteRadio(radio);
        }
    }

    private void MuteRadio(MusicRadioComponent radio)
    {
        if (radio.AudioStream is not { } stream || !TryComp<AudioComponent>(stream, out var audio))
            return;

        _audio.SetVolume(stream, float.NegativeInfinity, audio);
    }
}
