using System;
using System.Linq;
using Content.Shared._BlackM.Ghost.Customization;
using Content.Shared.Ghost;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._BlackM.Ghost.Customization;

public sealed class GhostCustomizationSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GhostComponent, ComponentInit>(OnGhostInit);
        SubscribeLocalEvent<GhostComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<GhostComponent, GhostCustomizationActionEvent>(OnCustomizationAction);
        SubscribeLocalEvent<GhostComponent, GhostSpriteSelectedMessage>(OnSpriteSelected);
    }

    private void OnGhostInit(EntityUid uid, GhostComponent component, ComponentInit args)
    {
        EnsureComp<GhostCustomizationComponent>(uid);
    }

    private void OnUiOpened(EntityUid uid, GhostComponent ghost, BoundUIOpenedEvent args)
    {
        UpdateUiState(uid);
    }

    private void OnCustomizationAction(EntityUid uid, GhostComponent ghost, GhostCustomizationActionEvent args)
    {
        if (!_ui.TryToggleUi(uid, GhostCustomizationUiKey.Key, args.Performer))
            return;

        UpdateUiState(uid);
        args.Handled = true;
    }

    private void UpdateUiState(EntityUid uid)
    {
        var ids = _proto.EnumeratePrototypes<GhostSpritePrototype>()
            .Select(p => p.ID)
            .OrderBy(id => id)
            .ToList();

        var selected = CompOrNull<GhostCustomizationComponent>(uid)?.SelectedSprite ?? string.Empty;

        _ui.SetUiState(uid, GhostCustomizationUiKey.Key,
            new GhostCustomizationBoundUserInterfaceState(ids, selected));
    }

    private void OnSpriteSelected(EntityUid uid, GhostComponent ghost, GhostSpriteSelectedMessage args)
    {
        if (!_proto.TryIndex<GhostSpritePrototype>(args.SpriteId, out var proto))
            return;

        var component = EnsureComp<GhostCustomizationComponent>(uid);
        component.SelectedSprite = proto.ID;
        Dirty(uid, component);

        UpdateUiState(uid);
    }
}
