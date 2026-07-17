using Content.Shared._BlackM.Access;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.DeviceLinking;
using Content.Shared.Interaction;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._BlackM.Access.Systems;

public abstract class SharedAccessButtonSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly SharedAccessCardHolderSystem CardHolder = default!;
    [Dependency] protected readonly SharedContainerSystem Container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AccessButtonComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(EntityUid uid, AccessButtonComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!HasComp<AccessCardHolderComponent>(args.Used))
            return;

        args.Handled = true;

        if (Timing.CurTime < component.NextPressAllowed)
            return;

        component.NextPressAllowed = Timing.CurTime + TimeSpan.FromSeconds(component.PressCooldown);

        TryPress(uid, args.Used, args.User, component);
    }

    protected virtual bool TryPress(EntityUid uid, EntityUid card, EntityUid user, AccessButtonComponent component)
    {
        return IsAllowed(uid, card, component);
    }

    protected bool IsAllowed(EntityUid uid, EntityUid card, AccessButtonComponent component)
    {
        var tags = CardHolder.GetAccessTags(card);

        if (!TryComp<DeviceLinkSourceComponent>(uid, out var source))
            return true;

        if (!source.Outputs.TryGetValue(component.Port, out var links) || links.Count == 0)
            return true;

        foreach (var target in links)
        {
            var candidates = new List<EntityUid> { target };

            if (Container.TryGetContainer(target, "board", out var boardContainer))
                candidates.AddRange(boardContainer.ContainedEntities);

            var anyReaderFound = false;
            var doorAllowed = true;

            foreach (var candidate in candidates)
            {
                if (!TryComp<AccessReaderComponent>(candidate, out var reader))
                    continue;

                if (reader.AccessLists.Count == 0)
                    continue;

                anyReaderFound = true;

                if (!CheckAccess(tags, reader))
                {
                    doorAllowed = false;
                    break;
                }
            }

            if (!anyReaderFound)
                continue;

            if (!doorAllowed)
                return false;
        }

        return true;
    }

    private static bool CheckAccess(HashSet<ProtoId<AccessLevelPrototype>> tags, AccessReaderComponent reader)
    {
        if (reader.AccessLists.Count == 0)
            return true;

        foreach (var set in reader.AccessLists)
        {
            if (set.IsSubsetOf(tags))
                return true;
        }

        return false;
    }
}
