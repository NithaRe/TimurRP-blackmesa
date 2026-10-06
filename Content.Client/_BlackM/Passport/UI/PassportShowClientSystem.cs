using Content.Client._BlackM.Passport.UI;
using Content.Shared._BlackM.Passport;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Client._BlackM.Passport;

public sealed class PassportShowClientSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private static readonly SoundSpecifier FlipSound  = new SoundCollectionSpecifier("PassportPageFlip");
    private static readonly SoundSpecifier OpenSound  = new SoundPathSpecifier("/Audio/_BlackM/Passport/cover_open.ogg");
    private static readonly SoundSpecifier CloseSound = new SoundPathSpecifier("/Audio/_BlackM/Passport/cover_close.ogg");

    private PassportWindow? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PassportShownEvent>(OnShown);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CloseWindow();
    }

    private void OnShown(PassportShownEvent ev)
    {
        CloseWindow();

        var window = new PassportWindow();
        window.OnPageTurned += PlayTurnSound;
        window.OnClose += () =>
        {
            if (_window == window)
                _window = null;
        };

        window.UpdateState(ev.State, EntityManager);
        _window = window;
        window.OpenCentered();
    }

    private void CloseWindow()
    {
        _window?.Close();
        _window = null;
    }

    private void PlayTurnSound(int from, int to)
    {
        var sound = to == 0 ? CloseSound : from == 0 ? OpenSound : FlipSound;
        _audio.PlayGlobal(sound, Filter.Local(), false, AudioParams.Default.WithVolume(-4f));
    }
}
