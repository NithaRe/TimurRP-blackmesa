using Content.Server.Actions;
using Content.Shared.Speech.Components;

namespace Content.Server._BlackM.Speech;

// Почему губы её вырезали?
public sealed class ScreamActionSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ScreamActionComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ScreamActionComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(EntityUid uid, ScreamActionComponent component, MapInitEvent args)
    {
        if (!HasComp<VocalComponent>(uid))
            return;

        _actions.AddAction(uid, ref component.ActionEntity, component.Action);
    }

    private void OnShutdown(EntityUid uid, ScreamActionComponent component, ComponentShutdown args)
    {
        if (component.ActionEntity != null)
            _actions.RemoveAction(uid, component.ActionEntity);
    }
}
