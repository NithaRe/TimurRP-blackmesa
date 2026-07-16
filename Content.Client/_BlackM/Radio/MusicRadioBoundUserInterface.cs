using Content.Shared._BlackM.Radio;
using Robust.Client.UserInterface;

namespace Content.Client._BlackM.Radio;

public sealed class MusicRadioBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private MusicRadioWindow? _window;

    public MusicRadioBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<MusicRadioWindow>();

        _window.OnTogglePlaying += () => SendMessage(new MusicRadioTogglePlayingMessage());
        _window.OnPrevTrack += () => SendMessage(new MusicRadioStepTrackMessage(-1));
        _window.OnNextTrack += () => SendMessage(new MusicRadioStepTrackMessage(1));
        _window.OnTrackSelected += index => SendMessage(new MusicRadioSetTrackMessage(index));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null || state is not MusicRadioBoundUserInterfaceState radioState)
            return;

        _window.UpdateState(radioState);
    }
}
