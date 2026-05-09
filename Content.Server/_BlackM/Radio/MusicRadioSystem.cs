using Content.Shared._BlackM.Radio;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;

namespace Content.Server._BlackM.Radio;

public sealed class MusicRadioSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem      _audio      = default!;
    [Dependency] private readonly SharedPopupSystem      _popup      = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MusicRadioComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MusicRadioComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<MusicRadioComponent, ActivateInWorldEvent>(OnActivate);
    }

    private void OnStartup(EntityUid uid, MusicRadioComponent radio, ComponentStartup args)
    {
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

        if (radio.Playing)
        {
            StopPlaying(uid, radio);
            _popup.PopupEntity(Loc.GetString("music-radio-off"), uid, args.User);
        }
        else
        {
            StartPlaying(uid, radio);
            _popup.PopupEntity(Loc.GetString("music-radio-on"), uid, args.User);
        }

        args.Handled = true;
    }

    private void StartPlaying(EntityUid uid, MusicRadioComponent radio)
    {
        if (radio.Playing)
            return;

        var resolved = _audio.ResolveSound(radio.Sound);
        if (resolved == null)
            return;

        var stream = _audio.PlayPvs(
            resolved,
            uid,
            AudioParams.Default
                .WithLoop(true)
                .WithVolume(radio.Volume)
                .WithMaxDistance(radio.Range));

        if (stream == null)
            return;

        radio.Stream  = stream.Value.Entity;
        radio.Playing = true;

        UpdateAppearance(uid, true);
    }

    private void StopPlaying(EntityUid uid, MusicRadioComponent radio)
    {
        if (!radio.Playing)
            return;

        if (radio.Stream != null)
        {
            _audio.Stop(radio.Stream.Value);
            radio.Stream = null;
        }

        radio.Playing = false;

        UpdateAppearance(uid, false);
    }

    private void UpdateAppearance(EntityUid uid, bool playing)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;

        _appearance.SetData(uid, MusicRadioVisuals.Playing, playing, appearance);
    }
}
