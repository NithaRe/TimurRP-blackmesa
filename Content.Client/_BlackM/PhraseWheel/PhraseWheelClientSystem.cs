// SPDX-License-Identifier: AGPL-3.0-or-later
using Content.Client.UserInterface.Systems.PhraseWheel;
using Content.Goobstation.Shared.PhraseWheel;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Client._BlackM.PhraseWheel;

public sealed class PhraseWheelClientSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PhraseWheelComponent, ComponentStartup>(OnCompAdded);
        SubscribeLocalEvent<PhraseWheelComponent, AfterAutoHandleStateEvent>(OnStateHandled);
        SubscribeLocalEvent<PhraseWheelComponent, ComponentShutdown>(OnCompRemoved);
    }

    private void OnCompAdded(Entity<PhraseWheelComponent> ent, ref ComponentStartup args)
    {
        if (_playerManager.LocalSession?.AttachedEntity != ent.Owner)
            return;
        UpdateVisibility();
    }

    private void OnStateHandled(Entity<PhraseWheelComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_playerManager.LocalSession?.AttachedEntity != ent.Owner)
            return;
        UpdateVisibility();
    }

    private void OnCompRemoved(Entity<PhraseWheelComponent> ent, ref ComponentShutdown args)
    {
        if (_playerManager.LocalSession?.AttachedEntity != ent.Owner)
            return;
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        var controller = IoCManager.Resolve<Robust.Client.UserInterface.IUserInterfaceManager>()
            .GetUIController<PhraseWheelUIController>();
        controller.UpdateButtonVisibility();
    }
}