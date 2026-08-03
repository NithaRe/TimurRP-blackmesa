using Content.Server.Popups;
using Content.Shared._BlackM.Access;
using Content.Shared._BlackM.Access.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Content.Shared.Examine;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Server._BlackM.Access;

public sealed class AccessCardHolderSystem : SharedAccessCardHolderSystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    private static readonly SoundSpecifier SoundInsert = new SoundPathSpecifier(
        "/Audio/_Goobstation/Items/handling/card_pickup.ogg")
    { Params = AudioParams.Default.WithVolume(-3f) };

    private static readonly SoundSpecifier SoundRemove = new SoundPathSpecifier(
        "/Audio/_Goobstation/Items/handling/card_drop.ogg")
    { Params = AudioParams.Default.WithVolume(-3f) };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AccessCardHolderComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<AccessCardHolderComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
        SubscribeLocalEvent<AccessCardHolderComponent, ExaminedEvent>(OnExamined);
    }

    private void OnInteractUsing(EntityUid uid, AccessCardHolderComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<BadgeComponent>(args.Used, out _))
            return;

        args.Handled = true;

        if (_container.TryGetContainer(uid, component.BadgeContainerId, out var existingContainer))
        {
            var usedState = GetBadgeProtoId(args.Used);
            foreach (var existing in existingContainer.ContainedEntities)
            {
                if (GetBadgeProtoId(existing) == usedState)
                {
                    _popup.PopupEntity(
                        Loc.GetString("access-card-holder-duplicate-badge",
                            ("badge", Name(args.Used))),
                        uid, args.User);
                    return;
                }
            }
        }

        if (!CanInsertBadge(uid, component))
        {
            _popup.PopupEntity(
                Loc.GetString("access-card-holder-full", ("max", component.MaxBadges)),
                uid, args.User);
            return;
        }

        if (!_container.TryGetContainer(uid, component.BadgeContainerId, out var container))
            return;

        if (!_container.Insert(args.Used, container))
        {
            _popup.PopupEntity(
                Loc.GetString("access-card-holder-insert-fail"),
                uid, args.User);
            return;
        }

        _audio.PlayPvs(SoundInsert, uid);
        _popup.PopupEntity(
            Loc.GetString("access-card-holder-badge-inserted",
                ("badge", Name(args.Used)),
                ("card", Name(uid))),
            uid, args.User);

        SyncAccess(uid, component);
    }

    private void OnGetAltVerbs(EntityUid uid, AccessCardHolderComponent component,
        GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        if (!_container.TryGetContainer(uid, component.BadgeContainerId, out var container))
            return;

        if (container.ContainedEntities.Count == 0)
            return;

        foreach (var badge in new List<EntityUid>(container.ContainedEntities))
        {
            var capturedBadge = badge;
            var capturedName = Name(badge);

            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("access-card-holder-remove-badge", ("badge", capturedName)),
                Priority = 1,
                Act = () => RemoveBadge(uid, capturedBadge, args.User, component)
            });
        }
    }

    private void OnExamined(EntityUid uid, AccessCardHolderComponent component, ExaminedEvent args)
    {
        if (!_container.TryGetContainer(uid, component.BadgeContainerId, out var container))
            return;

        if (container.ContainedEntities.Count == 0)
        {
            args.PushMarkup(Loc.GetString("access-card-holder-examine-empty"));
            return;
        }

        args.PushMarkup(Loc.GetString("access-card-holder-examine-header",
            ("count", container.ContainedEntities.Count),
            ("max", component.MaxBadges)));

        foreach (var badge in container.ContainedEntities)
        {
            if (!TryComp<BadgeComponent>(badge, out var badgeComp))
                continue;

            var hex = badgeComp.ExamineColor.ToHex();
            args.PushMarkup(Loc.GetString("access-card-holder-examine-entry",
                ("color", hex),
                ("badge", Name(badge))));
        }
    }

    private string GetBadgeProtoId(EntityUid badge)
    {
        return MetaData(badge).EntityPrototype?.ID?.ToLowerInvariant()
               ?? Name(badge).ToLowerInvariant();
    }

    private void RemoveBadge(EntityUid card, EntityUid badge, EntityUid user,
        AccessCardHolderComponent component)
    {
        if (!_container.TryGetContainer(card, component.BadgeContainerId, out var container))
            return;

        if (!container.Contains(badge))
        {
            _popup.PopupEntity(
                Loc.GetString("access-card-holder-remove-fail"),
                card, user);
            return;
        }

        if (!_container.Remove(badge, container))
        {
            _popup.PopupEntity(
                Loc.GetString("access-card-holder-remove-fail"),
                card, user);
            return;
        }

        _hands.PickupOrDrop(user, badge);

        _audio.PlayPvs(SoundRemove, card);
        _popup.PopupEntity(
            Loc.GetString("access-card-holder-badge-removed",
                ("badge", Name(badge)),
                ("card", Name(card))),
            card, user);

        SyncAccess(card, component);
    }
}
