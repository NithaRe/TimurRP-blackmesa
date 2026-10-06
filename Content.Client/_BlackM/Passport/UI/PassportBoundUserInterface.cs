using Content.Shared._BlackM.Passport;
using Robust.Client.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Client._BlackM.Passport.UI;

public sealed class PassportBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private PassportWindow? _window;

    private static readonly SoundSpecifier FlipSound  = new SoundCollectionSpecifier("PassportPageFlip");
    private static readonly SoundSpecifier OpenSound  = new SoundPathSpecifier("/Audio/_BlackM/Passport/cover_open.ogg");
    private static readonly SoundSpecifier CloseSound = new SoundPathSpecifier("/Audio/_BlackM/Passport/cover_close.ogg");

    public PassportBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        base.Open();
        _window = this.CreateWindow<PassportWindow>();
        _window.OnPageTurned += PlayTurnSound;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not PassportBoundUserInterfaceState cast)
            return;

        _window.UpdateState(cast, EntMan);
    }

    private void PlayTurnSound(int from, int to)
    {
        var sound = to == 0 ? CloseSound : from == 0 ? OpenSound : FlipSound;
        var audio = EntMan.System<SharedAudioSystem>();
        audio.PlayGlobal(sound, Filter.Local(), false, AudioParams.Default.WithVolume(-4f));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
}
