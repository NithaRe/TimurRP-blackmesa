// SPDX-License-Identifier: AGPL-3.0-or-later
using Content.Client.CombatMode;
using Content.Client.Movement.Components;
using Content.Client.Movement.Systems;
using Content.Shared.Camera;
using Content.Shared.CombatMode;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Client.Player;

namespace Content.Client._BlackM.Aiming;

public sealed class GunAimingSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly EyeCursorOffsetSystem _eyeOffset = default!;
    [Dependency] private readonly CombatModeSystem _combatMode = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        _combatMode.LocalPlayerCombatModeUpdated += OnCombatModeUpdated;

        SubscribeLocalEvent<HandsComponent, DidEquipHandEvent>(OnEquip);
        SubscribeLocalEvent<HandsComponent, DidUnequipHandEvent>(OnUnequip);
        SubscribeLocalEvent<HandsComponent, HandSelectedEvent>(OnSelected);

        SubscribeLocalEvent<CombatModeComponent, GetEyeOffsetEvent>(OnGetEyeOffset);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _combatMode.LocalPlayerCombatModeUpdated -= OnCombatModeUpdated;
    }

    private void OnCombatModeUpdated(bool inCombat) => UpdateAiming();
    private void OnEquip(EntityUid uid, HandsComponent component, ref DidEquipHandEvent args) => UpdateAiming();
    private void OnUnequip(EntityUid uid, HandsComponent component, ref DidUnequipHandEvent args) => UpdateAiming();
    private void OnSelected(EntityUid uid, HandsComponent component, ref HandSelectedEvent args) => UpdateAiming();

    private void UpdateAiming()
    {
        var uid = _playerManager.LocalEntity;
        if (uid == null)
            return;

        var inCombat = _combatMode.IsInCombatMode();
        var hasGun = HasGunInActiveHand(uid.Value);
        var comp = EnsureComp<EyeCursorOffsetComponent>(uid.Value);

        if (inCombat && hasGun)
        {
            comp.MaxOffset = 2f;
            comp.OffsetSpeed = 0.8f;
            comp.PvsIncrease = 0.3f;
        }
        else
        {
            // смещение
            comp.MaxOffset = 0f;
        }
    }

    private bool HasGunInActiveHand(EntityUid uid)
    {
        if (!_hands.TryGetActiveItem(uid, out var activeItem))
            return false;

        return HasComp<GunComponent>(activeItem.Value);
    }

    private void OnGetEyeOffset(EntityUid uid, CombatModeComponent component, ref GetEyeOffsetEvent args)
    {
        if (_playerManager.LocalEntity != uid)
            return;

        if (!TryComp<EyeCursorOffsetComponent>(uid, out var offsetComp))
            return;

        // если 0 ничего не делать
        if (offsetComp.MaxOffset <= 0)
            return;

        var offset = _eyeOffset.OffsetAfterMouse(uid, offsetComp);
        if (offset != null)
        {
            args.Offset += offset.Value;
        }
    }
}