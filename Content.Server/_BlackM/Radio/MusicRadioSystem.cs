using Content.Server.Popups;
using Content.Shared._BlackM.Radio;
using Content.Shared.Interaction;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.Radio;

public sealed class MusicRadioSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MusicRadioComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MusicRadioComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<MusicRadioComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<MusicRadioComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<MusicRadioComponent, MusicRadioTogglePlayingMessage>(OnTogglePlaying);
        SubscribeLocalEvent<MusicRadioComponent, MusicRadioSetTrackMessage>(OnSetTrack);
        SubscribeLocalEvent<MusicRadioComponent, MusicRadioStepTrackMessage>(OnStepTrack);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MusicRadioComponent>();
        while (query.MoveNext(out var uid, out var radio))
        {
            if (!radio.Playing || radio.AudioStream is not { } stream)
                continue;

            if (!Exists(stream) || Terminating(stream) || !_audio.IsPlaying(stream))
            {
                if (radio.AutoAdvance && radio.Tracks.Count > 0)
                {
                    var next = radio.CurrentTrack + 1 >= radio.Tracks.Count ? 0 : radio.CurrentTrack + 1;
                    SwitchTrack(uid, radio, next);
                }
                else
                {
                    StopPlaying(uid, radio);
                    Dirty(uid, radio);
                }
            }
        }
    }

    private void OnStartup(EntityUid uid, MusicRadioComponent radio, ComponentStartup args)
    {
        if (radio.CurrentTrack < 0 || radio.CurrentTrack >= radio.Tracks.Count)
            radio.CurrentTrack = 0;

        UpdateAppearance(uid, false);
    }

    private void OnShutdown(EntityUid uid, MusicRadioComponent radio, ComponentShutdown args)
    {
        StopPlaying(uid, radio);
    }

    private void OnActivate(EntityUid uid, MusicRadioComponent radio, ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<UserInterfaceComponent>(uid, out var ui)
            && TryComp<ActorComponent>(args.User, out var actorComp))
        {
            _ui.OpenUi(uid, MusicRadioUiKey.Key, actorComp.PlayerSession);
        }

        args.Handled = true;
    }

    private void OnUiOpened(EntityUid uid, MusicRadioComponent radio, BoundUIOpenedEvent args)
        => UpdateUiState(uid, radio);

    private void OnTogglePlaying(EntityUid uid, MusicRadioComponent radio, MusicRadioTogglePlayingMessage args)
    {
        if (radio.Playing)
        {
            StopPlaying(uid, radio);
            if (args.Actor is { } actor)
                _popup.PopupEntity(Loc.GetString("music-radio-off"), uid, actor);
        }
        else
        {
            StartPlaying(uid, radio);
            if (args.Actor is { } actor)
                _popup.PopupEntity(Loc.GetString("music-radio-on"), uid, actor);
        }

        Dirty(uid, radio);
        UpdateUiState(uid, radio);
    }

    private void OnSetTrack(EntityUid uid, MusicRadioComponent radio, MusicRadioSetTrackMessage args)
    {
        if (args.TrackIndex < 0 || args.TrackIndex >= radio.Tracks.Count)
            return;

        SwitchTrack(uid, radio, args.TrackIndex);
    }

    private void OnStepTrack(EntityUid uid, MusicRadioComponent radio, MusicRadioStepTrackMessage args)
    {
        if (radio.Tracks.Count == 0)
            return;

        var next = (radio.CurrentTrack + args.Direction) % radio.Tracks.Count;
        if (next < 0)
            next += radio.Tracks.Count;

        SwitchTrack(uid, radio, next);
    }

    private void SwitchTrack(EntityUid uid, MusicRadioComponent radio, int index)
    {
        radio.CurrentTrack = index;

        var wasPlaying = radio.Playing;
        StopPlaying(uid, radio);

        if (wasPlaying)
            StartPlaying(uid, radio);

        Dirty(uid, radio);
        UpdateUiState(uid, radio);
    }

    private void StartPlaying(EntityUid uid, MusicRadioComponent radio)
    {
        if (radio.Playing || radio.Tracks.Count == 0)
            return;

        var track = radio.Tracks[radio.CurrentTrack];
        var resolved = _audio.ResolveSound(track.Sound);
        if (resolved == null)
            return;

        var stream = _audio.PlayPvs(
            resolved,
            uid,
            AudioParams.Default
                .WithLoop(false)
                .WithVolume(radio.Volume)
                .WithMaxDistance(radio.Range));

        if (stream == null)
            return;

        radio.AudioStream = stream.Value.Entity;
        radio.Playing = true;

        UpdateAppearance(uid, true);
    }

    private void StopPlaying(EntityUid uid, MusicRadioComponent radio)
    {
        radio.AudioStream = _audio.Stop(radio.AudioStream);
        radio.Playing = false;

        UpdateAppearance(uid, false);
    }

    private void UpdateUiState(EntityUid uid, MusicRadioComponent radio)
    {
        var names = new List<string>(radio.Tracks.Count);
        foreach (var t in radio.Tracks)
            names.Add(t.Name);

        _ui.SetUiState(uid, MusicRadioUiKey.Key,
            new MusicRadioBoundUserInterfaceState(names, radio.CurrentTrack, radio.Playing));
    }

    private void UpdateAppearance(EntityUid uid, bool playing)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;

        _appearance.SetData(uid, MusicRadioVisuals.Playing, playing, appearance);
    }
}
