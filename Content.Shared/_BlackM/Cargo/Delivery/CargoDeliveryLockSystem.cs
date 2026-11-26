using Content.Shared.Access.Systems;
using Content.Shared.Examine;
using Content.Shared.Lock;
using Content.Shared.Popups;

namespace Content.Shared._BlackM.Cargo.Delivery;

public sealed class CargoDeliveryLockSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CargoDeliveryLockComponent, LockToggleAttemptEvent>(OnToggleAttempt);
        SubscribeLocalEvent<CargoDeliveryLockComponent, LockToggledEvent>(OnToggled);
        SubscribeLocalEvent<CargoDeliveryLockComponent, ExaminedEvent>(OnExamined);
    }

    public bool HasDepartmentAccess(Entity<CargoDeliveryLockComponent> ent, EntityUid user)
    {
        if (ent.Comp.Access.Count == 0)
            return true;

        var tags = _accessReader.FindAccessTags(user);
        foreach (var access in ent.Comp.Access)
        {
            if (tags.Contains(access))
                return true;
        }

        return false;
    }

    private void OnToggleAttempt(Entity<CargoDeliveryLockComponent> ent, ref LockToggleAttemptEvent args)
    {
        if (args.Cancelled || HasDepartmentAccess(ent, args.User))
            return;

        args.Cancelled = true;
        if (!args.Silent)
            _popup.PopupClient(Loc.GetString("cargo-delivery-lock-denied", ("name", ent.Comp.DestinationName)), ent, args.User);
    }

    private void OnToggled(Entity<CargoDeliveryLockComponent> ent, ref LockToggledEvent args)
    {
        if (!args.Locked)
            RemCompDeferred<CargoDeliveryLockComponent>(ent);
    }

    private void OnExamined(Entity<CargoDeliveryLockComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("cargo-delivery-lock-examine", ("name", ent.Comp.DestinationName)));
    }
}
