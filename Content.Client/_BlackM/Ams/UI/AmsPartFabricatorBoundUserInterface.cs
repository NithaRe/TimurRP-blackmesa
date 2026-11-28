using Content.Goobstation.Shared.Lathe;
using Content.Shared.Lathe;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._BlackM.Ams.UI;

[UsedImplicitly]
public sealed class AmsPartFabricatorBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private AmsPartFabricatorWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<AmsPartFabricatorWindow>();
        _window.SetEntity(Owner);
        _window.Fabricate += recipe => SendMessage(new LatheQueueRecipeMessage(recipe, 1));
        _window.ClearQueue += () => SendMessage(new LatheQueueResetMessage());
        SendMessage(new LatheSyncRequestMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is LatheUpdateState lathe)
            _window?.UpdateState(lathe);
    }
}
