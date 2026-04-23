// SPDX-FileCopyrightText: 2026 BlackM Project
// SPDX-License-Identifier: CC-BY-SA-3.0

using Content.Shared.Actions;
using Content.Shared.Clothing;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._BlackM.ClothingToggle;

public sealed class ClothingStateToggleSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly ClothingSystem _clothing     = default!;
    [Dependency] private readonly SharedAudioSystem _audio     = default!;
    [Dependency] private readonly IGameTiming _timing          = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ClothingStateToggleComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<ClothingStateToggleComponent, ClothingGotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<ClothingStateToggleComponent, ClothingStateToggleActionEvent>(OnToggle);


    }

    private void OnEquipped(EntityUid uid, ClothingStateToggleComponent comp, ClothingGotEquippedEvent args)
    {
        _actions.AddAction(args.Wearer, ref comp.ActionEntity, comp.Action, uid);
    }

    private void OnUnequipped(EntityUid uid, ClothingStateToggleComponent comp, ClothingGotUnequippedEvent args)
    {
        if (comp.ActionEntity == null || !Exists(comp.ActionEntity.Value))
            return;

        _actions.RemoveAction(comp.ActionEntity.Value);
        comp.ActionEntity = null;
    }

    private void OnToggle(EntityUid uid, ClothingStateToggleComponent comp, ClothingStateToggleActionEvent args)
    {
        var now = _timing.CurTime;
        if (now - comp.LastToggleTime < TimeSpan.FromSeconds(comp.ToggleCooldown))
            return;

        comp.LastToggleTime = now;
        comp.IsOpen = !comp.IsOpen;

        _clothing.SetEquippedPrefix(uid, comp.IsOpen ? comp.OpenPrefix : comp.ClosedPrefix);

        if (comp.ToggleSound != null)
            _audio.PlayPvs(comp.ToggleSound, uid);
    }


}
