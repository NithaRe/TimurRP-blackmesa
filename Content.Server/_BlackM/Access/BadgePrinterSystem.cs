using System;
using System.Collections.Generic;
using Content.Server.Popups;
using Content.Shared._BlackM.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Content.Shared.Paper;
using Content.Shared.UserInterface;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.Access;

public sealed class BadgePrinterSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly AccessCardHolderSystem _accessCardHolder = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BadgePrinterComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BadgePrinterComponent, EntInsertedIntoContainerMessage>(OnContainerModified);
        SubscribeLocalEvent<BadgePrinterComponent, EntRemovedFromContainerMessage>(OnContainerModified);
        SubscribeLocalEvent<BadgePrinterComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);

        SubscribeLocalEvent<BadgePrinterComponent, ActivatableUIOpenAttemptEvent>(OnUiOpenAttempt);
        SubscribeLocalEvent<BadgePrinterComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<BadgePrinterComponent, BadgePrinterPrintMessage>(OnPrint);
        SubscribeLocalEvent<BadgePrinterComponent, BadgePrinterEjectCardMessage>(OnEjectCard);
    }

    private void OnUiOpenAttempt(EntityUid uid, BadgePrinterComponent component, ActivatableUIOpenAttemptEvent args)
    {
        if (!HasComp<AccessReaderComponent>(uid))
            return;

        if (_accessReader.IsAllowed(args.User, uid))
            return;

        args.Cancel();
        _popup.PopupEntity(Loc.GetString("badge-printer-access-denied"), uid, args.User);
    }

    private void OnStartup(EntityUid uid, BadgePrinterComponent component, ComponentStartup args)
    {
        UpdateUi(uid, component);
    }

    private void OnInsertAttempt(EntityUid uid, BadgePrinterComponent component, ItemSlotInsertAttemptEvent args)
    {
        if (args.Slot.ID != component.CardSlotId)
            return;

        if (!HasComp<AccessCardHolderComponent>(args.Item))
            args.Cancelled = true;
    }

    private void OnContainerModified(EntityUid uid, BadgePrinterComponent component, ContainerModifiedMessage args)
    {
        UpdateUi(uid, component);
    }

    private void OnUiOpened(EntityUid uid, BadgePrinterComponent component, BoundUIOpenedEvent args)
    {
        UpdateUi(uid, component);
    }

    private void OnEjectCard(EntityUid uid, BadgePrinterComponent component, BadgePrinterEjectCardMessage args)
    {
        var slot = GetCardSlot(uid, component);
        if (slot == null)
            return;

        _itemSlots.TryEjectToHands(uid, slot, args.Actor);
    }

    private ItemSlot? GetCardSlot(EntityUid uid, BadgePrinterComponent component)
    {
        if (!TryComp<ItemSlotsComponent>(uid, out var slots))
            return null;

        return slots.Slots.GetValueOrDefault(component.CardSlotId);
    }

    private (string? Name, string? Job) GetCardOwnerInfo(EntityUid cardUid)
    {
        string? name = null;
        string? job = null;

        if (TryComp<IdCardComponent>(cardUid, out var idCard))
        {
            if (!string.IsNullOrWhiteSpace(idCard.FullName))
                name = idCard.FullName;
            if (!string.IsNullOrWhiteSpace(idCard.LocalizedJobTitle))
                job = idCard.LocalizedJobTitle;
        }

        if (TryComp<AccessCardHolderComponent>(cardUid, out var holder))
        {
            if (name == null && !string.IsNullOrWhiteSpace(holder.FullName))
                name = holder.FullName;
            if (job == null && !string.IsNullOrWhiteSpace(holder.JobTitle))
                job = holder.JobTitle;
        }

        return (name, job);
    }

    private void UpdateUi(EntityUid uid, BadgePrinterComponent component)
    {
        var slot = GetCardSlot(uid, component);
        var card = slot?.Item;

        string? holderName = null;
        string? holderJob = null;
        var hasCard = false;

        if (card is { } cardUid && HasComp<AccessCardHolderComponent>(cardUid))
        {
            hasCard = true;
            var (name, job) = GetCardOwnerInfo(cardUid);
            holderName = name ?? Loc.GetString("badge-printer-unknown-name");
            holderJob = job ?? Loc.GetString("badge-printer-unknown-job");
        }

        var options = new List<BadgePrinterOptionData>();
        foreach (var entry in component.AvailableBadges)
        {
            if (!_proto.TryIndex(entry.Proto, out var badgeProto))
                continue;

            int? remaining = null;
            if (entry.Max is { } max)
            {
                var printed = component.PrintedCounts.GetValueOrDefault(entry.Proto.Id);
                remaining = Math.Max(0, max - printed);
            }

            options.Add(new BadgePrinterOptionData(
                entry.Proto,
                badgeProto.Name,
                badgeProto.Description,
                entry.IconRsi,
                entry.IconState,
                remaining));
        }

        var state = new BadgePrinterBuiState(hasCard, holderName, holderJob, options);
        _ui.SetUiState(uid, BadgePrinterUiKey.Key, state);
    }

    private void OnPrint(EntityUid uid, BadgePrinterComponent component, BadgePrinterPrintMessage args)
    {
        var user = args.Actor;

        if (HasComp<AccessReaderComponent>(uid) && !_accessReader.IsAllowed(user, uid))
        {
            _popup.PopupEntity(Loc.GetString("badge-printer-access-denied"), uid, user);
            _audio.PlayPvs(component.SoundDeny, uid);
            return;
        }

        var curTime = _timing.CurTime;
        if (curTime < component.NextPrintTime)
        {
            var remaining = (component.NextPrintTime - curTime).TotalSeconds;
            _popup.PopupEntity(Loc.GetString("badge-printer-on-cooldown", ("seconds", Math.Ceiling(remaining))), uid, user);
            _audio.PlayPvs(component.SoundDeny, uid);
            return;
        }

        if (args.SelectedBadgeProtoIds.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("badge-printer-no-selection"), uid, user);
            _audio.PlayPvs(component.SoundDeny, uid);
            return;
        }

        if (args.SelectedBadgeProtoIds.Count > component.MaxBadgesPerPrint)
        {
            _popup.PopupEntity(Loc.GetString("badge-printer-too-many", ("max", component.MaxBadgesPerPrint)), uid, user);
            _audio.PlayPvs(component.SoundDeny, uid);
            return;
        }

        var entriesByProto = new Dictionary<string, BadgePrinterEntry>();
        foreach (var entry in component.AvailableBadges)
            entriesByProto[entry.Proto.Id] = entry;

        var slot = GetCardSlot(uid, component);
        var card = slot?.Item;

        if (card is not { } cardUid || !TryComp<AccessCardHolderComponent>(cardUid, out var holder))
        {
            _popup.PopupEntity(Loc.GetString("badge-printer-no-card"), uid, user);
            _audio.PlayPvs(component.SoundDeny, uid);
            return;
        }

        if (!_container.TryGetContainer(cardUid, holder.BadgeContainerId, out var container))
        {
            _popup.PopupEntity(Loc.GetString("badge-printer-no-card"), uid, user);
            _audio.PlayPvs(component.SoundDeny, uid);
            return;
        }

        var toSpawn = new List<string>();
        var outOfStock = new List<string>();

        foreach (var id in args.SelectedBadgeProtoIds)
        {
            if (!entriesByProto.TryGetValue(id, out var entry))
                continue;

            if (entry.Max is { } max)
            {
                var printed = component.PrintedCounts.GetValueOrDefault(id);
                if (printed >= max)
                {
                    outOfStock.Add(id);
                    continue;
                }
            }

            toSpawn.Add(id);
        }

        if (toSpawn.Count == 0)
        {
            _popup.PopupEntity(
                outOfStock.Count > 0
                    ? Loc.GetString("badge-printer-out-of-stock")
                    : Loc.GetString("badge-printer-no-selection"),
                uid, user);
            _audio.PlayPvs(component.SoundDeny, uid);
            return;
        }

        var freeSlots = holder.MaxBadges - container.ContainedEntities.Count;
        if (freeSlots <= 0)
        {
            _popup.PopupEntity(Loc.GetString("access-card-holder-full", ("max", holder.MaxBadges)), uid, user);
            _audio.PlayPvs(component.SoundDeny, uid);
            return;
        }

        if (toSpawn.Count > freeSlots)
        {
            outOfStock.AddRange(toSpawn.GetRange(freeSlots, toSpawn.Count - freeSlots));
            toSpawn.RemoveRange(freeSlots, toSpawn.Count - freeSlots);
        }

        string holderName = Loc.GetString("badge-printer-unknown-name");
        string holderJob = Loc.GetString("badge-printer-unknown-job");

        var (name, job) = GetCardOwnerInfo(cardUid);
        if (name != null)
            holderName = name;
        if (job != null)
            holderJob = job;

        var spawnCoords = Transform(uid).Coordinates;
        var printedNames = new List<string>();
        var printedCount = 0;

        foreach (var protoId in toSpawn)
        {
            var badgeUid = Spawn(protoId, spawnCoords);

            if (!_container.Insert(badgeUid, container))
            {
                _transform.SetCoordinates(badgeUid, spawnCoords.Offset(_random.NextVector2(0.05f, 0.2f)));
                continue;
            }

            printedNames.Add(Name(badgeUid));
            component.PrintedCounts[protoId] = component.PrintedCounts.GetValueOrDefault(protoId) + 1;
            printedCount++;
        }

        if (printedCount == 0)
        {
            _popup.PopupEntity(Loc.GetString("access-card-holder-full", ("max", holder.MaxBadges)), uid, user);
            _audio.PlayPvs(component.SoundDeny, uid);
            return;
        }

        _accessCardHolder.SyncAccess(cardUid, holder);

        var paperUid = Spawn(component.PaperPrototype, spawnCoords.Offset(_random.NextVector2(0.05f, 0.2f)));
        if (TryComp<PaperComponent>(paperUid, out var paper))
        {
            var reason = string.IsNullOrWhiteSpace(args.Reason)
                ? Loc.GetString("badge-printer-no-reason")
                : args.Reason.Trim();

            var content = Loc.GetString("badge-printer-receipt-content",
                ("recipient", holderName),
                ("job", holderJob),
                ("issuer", Name(user)),
                ("reason", reason),
                ("badges", string.Join(", ", printedNames)),
                ("date", _timing.CurTime.ToString()));

            paper.Content = content;
            Dirty(paperUid, paper);
        }

        component.NextPrintTime = curTime + component.PrintDelay;

        _audio.PlayPvs(component.SoundPrint, uid);
        _popup.PopupEntity(Loc.GetString("badge-printer-print-success", ("count", printedCount)), uid, user);

        if (outOfStock.Count > 0)
            _popup.PopupEntity(Loc.GetString("badge-printer-out-of-stock"), uid, user);

        UpdateUi(uid, component);
    }
}
