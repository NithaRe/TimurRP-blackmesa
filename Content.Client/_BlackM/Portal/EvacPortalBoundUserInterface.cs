using Content.Shared._BlackM.Portal;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._BlackM.Portal;

[UsedImplicitly]
public sealed class EvacPortalBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private EvacPortalWindow? _window;

    public EvacPortalBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<EvacPortalWindow>();
        _window.OnLaunchPressed += OnLaunchPressed;
        _window.OnTeleportPressed += OnTeleportPressed;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not EvacPortalBuiState portalState)
            return;

        _window?.UpdateState(portalState);
    }

    private void OnLaunchPressed()
    {
        SendMessage(new EvacPortalLaunchMessage());
    }

    private void OnTeleportPressed()
    {
        SendMessage(new EvacPortalTeleportMessage());
    }
}