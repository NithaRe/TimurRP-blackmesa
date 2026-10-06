using Content.Server.Popups;
using Content.Shared._BlackM.Passport;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Player;

namespace Content.Server._BlackM.Passport;

public sealed class PassportShowSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands     = default!;
    [Dependency] private readonly InventorySystem   _inventory = default!;
    [Dependency] private readonly PopupSystem       _popup     = default!;
    [Dependency] private readonly PassportSystem    _passport  = default!;

    private const string PassportSlot = "passport";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HumanoidAppearanceComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
    }

    private void OnGetVerbs(EntityUid target, HumanoidAppearanceComponent humanoid, GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;

        if (user == target || !HasComp<ActorComponent>(target))
            return;

        if (!TryFindPassport(user, out _))
            return;

        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("passport-verb-show"),
            Priority = 5,
            Act = () => ShowPassport(user, target),
        });
    }

    private void ShowPassport(EntityUid user, EntityUid target)
    {
        if (!TryFindPassport(user, out var passport) || !TryComp<PassportComponent>(passport, out var comp))
            return;

        if (!TryComp<ActorComponent>(target, out var actor))
            return;

        RaiseNetworkEvent(new PassportShownEvent(_passport.BuildState(comp)), actor.PlayerSession);

        var msg = Loc.GetString("passport-show-popup",
            ("user", Identity.Name(user, EntityManager)),
            ("target", Identity.Name(target, EntityManager)));

        _popup.PopupEntity(msg, user, PopupType.Small);
    }

    private bool TryFindPassport(EntityUid user, out EntityUid passport)
    {
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (IsFilledPassport(held))
            {
                passport = held;
                return true;
            }
        }

        if (_inventory.TryGetSlotEntity(user, PassportSlot, out var worn) && IsFilledPassport(worn.Value))
        {
            passport = worn.Value;
            return true;
        }

        passport = default;
        return false;
    }

    private bool IsFilledPassport(EntityUid uid)
        => TryComp<PassportComponent>(uid, out var comp) && comp.IsBound;
}
