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
    [Dependency] private readonly IGameTiming _timing = default!;

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
            if (!radio.Playing)
                continue;

            if (radio.AutoAdvance
                && radio.TrackEndTime is { } endTime
                && _timing.CurTime >= endTime)
            {
                var next = radio.CurrentTrack + 1;
                if (next >= radio.Tracks.Count)
                    next = 0;

                SwitchTrack(uid, radio, next);
                continue;
            }

            radio.WatchdogAccumulator += frameTime;
            if (radio.WatchdogAccumulator < radio.WatchdogInterval)
                continue;

            radio.WatchdogAccumulator = 0f;

            var streamMissing = radio.Stream is not { } streamUid
                                 || !Exists(streamUid)
                                 || Terminating(streamUid);

            if (streamMissing)
            {
                radio.Playing = false;
                StartPlaying(uid, radio);
            }
        }
    }

    private void OnStartup(EntityUid uid, MusicRadioComponent radio, ComponentStartup args)
    {
        if (radio.CurrentTrack < 0 || radio.CurrentTrack >= radio.Tracks.Count)
            radio.CurrentTrack = 0;

        StartPlaying(uid, radio);
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
    {
        UpdateUiState(uid, radio);
    }

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

        UpdateUiState(uid, radio);
    }

    private void StartPlaying(EntityUid uid, MusicRadioComponent radio)
    {
        if (radio.Playing)
            return;

        if (radio.Tracks.Count == 0)
            return;

        var track = radio.Tracks[radio.CurrentTrack];

        var resolved = _audio.ResolveSound(track.Sound);
        if (resolved == null)
            return;

        var length = _audio.GetAudioLength(resolved);
        radio.TrackEndTime = length > TimeSpan.Zero
            ? _timing.CurTime + length
            : null;

        var stream = _audio.PlayPvs(
            resolved,
            uid,
            AudioParams.Default
                .WithLoop(false)
                .WithVolume(radio.Volume)
                .WithMaxDistance(radio.Range));

        if (stream == null)
            return;

        radio.Stream = stream.Value.Entity;
        radio.Playing = true;
        radio.WatchdogAccumulator = 0f;

        UpdateAppearance(uid, true);
    }

    private void StopPlaying(EntityUid uid, MusicRadioComponent radio)
    {
        if (radio.Stream != null)
        {
            _audio.Stop(radio.Stream.Value);
            radio.Stream = null;
        }

        radio.Playing = false;
        radio.TrackEndTime = null;

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

        _appearance.SetData(uid, Content.Shared._BlackM.Radio.MusicRadioVisuals.Playing, playing, appearance);
    }
}
