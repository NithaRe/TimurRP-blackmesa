using Content.Client.UserInterface.Systems.PhraseWheel;
using Content.Shared._BlackM.PhraseWheel;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Client.Player;
using Robust.Shared.IoC;

namespace Content.Client._BlackM.PhraseWheel;

public sealed class PhraseWheelClientSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private bool _pendingVisibilityUpdate = false;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PhraseWheelComponent, ComponentStartup>(OnCompAdded);
        SubscribeLocalEvent<PhraseWheelComponent, AfterAutoHandleStateEvent>(OnStateHandled);
        SubscribeLocalEvent<PhraseWheelComponent, ComponentShutdown>(OnCompRemoved);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        _playerManager.LocalPlayerAttached += OnLocalPlayerAttached;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.LocalPlayerAttached -= OnLocalPlayerAttached;
    }

    private void OnLocalPlayerAttached(EntityUid uid)
    {
        _pendingVisibilityUpdate = true;
    }

    public override void FrameUpdate(float frameTime)
    {
        if (!_pendingVisibilityUpdate) return;
        var uid = _playerManager.LocalSession?.AttachedEntity;
        if (uid == null) return;
        if (!HasComp<PhraseWheelComponent>(uid.Value)) return;
        _pendingVisibilityUpdate = false;
        UpdateVisibility();
    }

    private void OnCompAdded(Entity<PhraseWheelComponent> ent, ref ComponentStartup args)
    {
        if (_playerManager.LocalSession?.AttachedEntity != ent.Owner) return;
        UpdateVisibility();
    }

    private void OnStateHandled(Entity<PhraseWheelComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_playerManager.LocalSession?.AttachedEntity != ent.Owner) return;
        UpdateVisibility();
    }

    private void OnCompRemoved(Entity<PhraseWheelComponent> ent, ref ComponentShutdown args)
    {
        if (_playerManager.LocalSession?.AttachedEntity != ent.Owner) return;
        UpdateVisibility();
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        var localUid = _playerManager.LocalSession?.AttachedEntity;
        if (localUid == null || args.Target != localUid.Value) return;

        var controller = GetController();
        if (controller == null) return;

        if (args.NewMobState == MobState.Critical || args.NewMobState == MobState.Dead)
            controller.ForceClose();
        else
            controller.UpdateButtonVisibility();
    }

    private void UpdateVisibility() => GetController()?.UpdateButtonVisibility();

    private PhraseWheelUIController? GetController()
    {
        return IoCManager.Resolve<Robust.Client.UserInterface.IUserInterfaceManager>()
            .GetUIController<PhraseWheelUIController>();
    }
}