using Content.Shared.Buckle.Components;
using Content.Shared.Popups;

namespace Content.Shared._BlackM.Monorail;

public sealed class MonorailSeatSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MonorailSeatComponent, StrapAttemptEvent>(OnStrapAttempt);
        SubscribeLocalEvent<MonorailSeatComponent, UnstrapAttemptEvent>(OnUnstrapAttempt);
        SubscribeLocalEvent<MonorailSeatComponent, UnstrappedEvent>(OnUnstrapped);
    }

    public bool IsLocked(EntityUid seat)
    {
        var parent = Transform(seat).ParentUid;
        return TryComp<MonorailCarComponent>(parent, out var car)
               && car.Moving
               && car.LockPassengersWhileMoving;
    }

    private void OnStrapAttempt(Entity<MonorailSeatComponent> ent, ref StrapAttemptEvent args)
    {
        if (args.Cancelled || !IsLocked(ent))
            return;

        var car = Transform(ent).ParentUid;
        if (Transform(args.Buckle.Owner).ParentUid == car)
            return;

        args.Cancelled = true;

        if (args.Popup && args.User is { } user)
            _popup.PopupClient(Loc.GetString("monorail-seat-board-moving"), ent, user);
    }

    private void OnUnstrapAttempt(Entity<MonorailSeatComponent> ent, ref UnstrapAttemptEvent args)
    {
        if (args.Cancelled || args.User is not { } user || !IsLocked(ent))
            return;

        args.Cancelled = true;

        if (args.Popup)
            _popup.PopupClient(Loc.GetString("monorail-seat-unbuckle-moving"), ent, user);
    }

    private void OnUnstrapped(Entity<MonorailSeatComponent> ent, ref UnstrappedEvent args)
    {
        var buckle = args.Buckle.Owner;
        if (TerminatingOrDeleted(buckle))
            return;

        var parent = Transform(buckle).ParentUid;
        if (HasComp<MonorailCarComponent>(parent) && !TerminatingOrDeleted(parent))
            return;

        if (HasComp<MonorailSeatComponent>(parent))
            _xform.AttachToGridOrMap(buckle);
    }
}
