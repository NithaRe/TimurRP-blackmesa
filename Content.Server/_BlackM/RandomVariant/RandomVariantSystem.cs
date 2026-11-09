using Content.Shared._BlackM.RandomVariant;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._BlackM.RandomVariant;

public sealed class RandomVariantSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    // Starting gear equips the item right after its MapInit, so the swap waits for the next update.
    private readonly List<EntityUid> _pending = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RandomVariantComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<RandomVariantComponent> ent, ref MapInitEvent args)
    {
        _pending.Add(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pending.Count == 0)
            return;

        var batch = _pending.ToArray();
        _pending.Clear();

        foreach (var uid in batch)
        {
            if (TerminatingOrDeleted(uid) ||
                !TryComp<RandomVariantComponent>(uid, out var comp) ||
                comp.Variants.Count == 0)
                continue;

            Replace(uid, _random.Pick(comp.Variants));
        }
    }

    private void Replace(EntityUid uid, EntProtoId proto)
    {
        if (!_container.TryGetContainingContainer((uid, null, null), out var container))
        {
            Spawn(proto.Id, Transform(uid).Coordinates);
            Del(uid);
            return;
        }

        // Container-level swap: TryUnequip on a jumpsuit would also drop id, pockets and belt.
        var owner = container.Owner;
        var id = container.ID;
        _container.Remove(uid, container, force: true);
        Del(uid);
        SpawnInContainerOrDrop(proto.Id, owner, id);
    }
}
