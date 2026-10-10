using Content.Client.UserInterface.Systems.PhraseWheel;
using Content.Shared._BlackM.PhraseWheel;
using Content.Shared.Input;
using Content.Shared.Mobs;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.GameStates;
using Robust.Shared.Input.Binding;
using Robust.Shared.Maths;

namespace Content.Client._BlackM.PhraseWheel;

public sealed class PhraseWheelClientSystem : SharedPhraseWheelSystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PhraseWheelComponent, ComponentStartup>(OnCompAdded);
        SubscribeLocalEvent<PhraseWheelComponent, ComponentShutdown>(OnCompRemoved);
        SubscribeLocalEvent<PhraseWheelComponent, AfterAutoHandleStateEvent>(OnStateHandled);
        SubscribeLocalEvent<PhraseWheelComponent, MobStateChangedEvent>(OnMobStateChanged);

        _playerManager.LocalPlayerAttached += OnLocalPlayerAttached;

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.OpenPhraseWheel,
                InputCmdHandler.FromDelegate(_ => Controller.ToggleWindowFromKeybind()))
            .Register<PhraseWheelClientSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.LocalPlayerAttached -= OnLocalPlayerAttached;
        CommandBinds.Unregister<PhraseWheelClientSystem>();
    }

    private PhraseWheelUIController Controller => _ui.GetUIController<PhraseWheelUIController>();

    public bool TryGetLocal(out EntityUid uid, out PhraseWheelComponent comp)
    {
        uid = default;
        comp = default!;

        if (_playerManager.LocalSession?.AttachedEntity is not { } attached
            || !TryComp<PhraseWheelComponent>(attached, out var found))
        {
            return false;
        }

        uid = attached;
        comp = found;
        return true;
    }

    public float GetLocalCooldownSeconds()
    {
        return TryGetLocal(out _, out var comp)
            ? (float) GetCooldownRemaining(comp).TotalSeconds
            : 0f;
    }

    public bool TryRequestPlay(PhraseWheelEntryPrototype phrase, Color? customColor)
    {
        if (!TryGetLocal(out var uid, out var comp))
            return false;

        if (!IsCategoryAllowed(comp, phrase.Category) || !CanUse(uid, comp) || !CanSend(uid, phrase))
            return false;

        StartCooldown(comp);

        RaiseNetworkEvent(new PlayPhraseWheelMessage
        {
            Phrase = phrase.ID,
            CustomColor = phrase.AllowCustomColor ? customColor : null,
        });

        return true;
    }

    private void OnCompAdded(Entity<PhraseWheelComponent> ent, ref ComponentStartup args)
    {
        if (_playerManager.LocalSession?.AttachedEntity == ent.Owner)
            Controller.UpdateButtonVisibility();
    }

    private void OnStateHandled(Entity<PhraseWheelComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_playerManager.LocalSession?.AttachedEntity == ent.Owner)
            Controller.UpdateButtonVisibility();
    }

    private void OnCompRemoved(Entity<PhraseWheelComponent> ent, ref ComponentShutdown args)
    {
        if (_playerManager.LocalSession?.AttachedEntity != ent.Owner)
            return;

        Controller.ForceClose();
        Controller.UpdateButtonVisibility();
    }

    private void OnMobStateChanged(EntityUid uid, PhraseWheelComponent comp, MobStateChangedEvent args)
    {
        if (_playerManager.LocalSession?.AttachedEntity != uid)
            return;

        if (args.NewMobState != MobState.Alive)
            Controller.ForceClose();

        Controller.UpdateButtonVisibility();
    }

    private void OnLocalPlayerAttached(EntityUid uid)
    {
        Controller.HandleAttachedEntityChanged();
    }
}
