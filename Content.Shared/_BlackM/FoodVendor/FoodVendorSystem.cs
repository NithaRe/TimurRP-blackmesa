using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Emag.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.VendingMachines;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;

namespace Content.Shared._BlackM.FoodVendor;

public sealed class FoodVendorSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedVendingMachineSystem _vending = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly AccessReaderSystem _access = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FoodVendorComponent, BoundUserInterfaceMessageAttempt>(OnMessageAttempt,
            after: new[] { typeof(SharedInteractionSystem) });
        SubscribeLocalEvent<FoodVendorComponent, ExaminedEvent>(OnExamined);
    }

    public int GetTaken(EntityUid user, string group)
    {
        if (!TryComp<FoodVendorUserComponent>(user, out var comp))
            return 0;

        return comp.Taken.TryGetValue(group, out var taken) ? taken : 0;
    }

    public static string LockedMessage(FoodVendorComponent comp)
    {
        return comp.LockWhenCookPresent
            ? "blackm-food-vendor-locked-cook"
            : "blackm-food-vendor-locked-no-cook";
    }

    private void OnMessageAttempt(Entity<FoodVendorComponent> ent, ref BoundUserInterfaceMessageAttempt args)
    {
        if (args.Cancelled || args.Message is not VendingMachineEjectMessage msg)
            return;

        if (!TryComp<VendingMachineComponent>(ent, out var vend) || HasComp<EmaggedComponent>(ent))
            return;

        var actor = args.Actor;

        if (ent.Comp.Locked)
        {
            _popup.PopupClient(Loc.GetString(LockedMessage(ent.Comp)), ent, actor);
            _vending.Deny((ent, vend), actor);
            args.Cancel();
            return;
        }

        if (ent.Comp.Limit <= 0)
            return;

        var taken = GetTaken(actor, ent.Comp.Group);
        if (taken >= ent.Comp.Limit)
        {
            _popup.PopupClient(Loc.GetString("blackm-food-vendor-limit", ("limit", ent.Comp.Limit)), ent, actor);
            _vending.Deny((ent, vend), actor);
            args.Cancel();
            return;
        }

        // Count on the server only, and only if the vend will go through.
        if (_net.IsClient || !WillVend(ent, vend, msg, actor))
            return;

        var user = EnsureComp<FoodVendorUserComponent>(actor);
        user.Taken[ent.Comp.Group] = taken + 1;
        Dirty(actor, user);

        _popup.PopupEntity(Loc.GetString("blackm-food-vendor-left",
            ("left", ent.Comp.Limit - taken - 1), ("limit", ent.Comp.Limit)), ent, actor);
    }

    // Mirrors the checks in SharedVendingMachineSystem.TryEjectVendorItem.
    private bool WillVend(EntityUid uid, VendingMachineComponent vend, VendingMachineEjectMessage msg, EntityUid actor)
    {
        if (vend.Ejecting || vend.Broken || !_power.IsPowered(uid))
            return false;

        if (TryComp<AccessReaderComponent>(uid, out var reader) && !_access.IsAllowed(actor, uid, reader))
            return false;

        Dictionary<string, VendingMachineInventoryEntry>? inventory = msg.Type switch
        {
            InventoryType.Regular => vend.Inventory,
            InventoryType.Contraband when vend.Contraband => vend.ContrabandInventory,
            _ => null,
        };

        return inventory != null
               && inventory.TryGetValue(msg.ID, out var entry)
               && entry.Amount > 0;
    }

    private void OnExamined(Entity<FoodVendorComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (ent.Comp.Locked)
            args.PushMarkup($"[color=red]{Loc.GetString(LockedMessage(ent.Comp))}[/color]");
        else
            args.PushMarkup(Loc.GetString("blackm-food-vendor-examine-open"));

        if (ent.Comp.Limit > 0)
        {
            args.PushMarkup(Loc.GetString("blackm-food-vendor-examine-limit",
                ("taken", GetTaken(args.Examiner, ent.Comp.Group)),
                ("limit", ent.Comp.Limit)));
        }
    }
}
