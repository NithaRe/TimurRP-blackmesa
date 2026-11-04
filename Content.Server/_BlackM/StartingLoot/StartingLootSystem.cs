using System.Linq;
using Content.Shared._BlackM.StartingLoot;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._BlackM.StartingLoot;

public sealed class StartingLootSystem : EntitySystem
{
    private const string PoolId = "BlackMDefault";
    private const bool GiveOnLateJoin = true;

    [Dependency] private readonly StartingLootVoteSystem _vote = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    private readonly HashSet<NetUserId> _givenThisRound = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _givenThisRound.Clear();
    }

    private void OnSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId == null)
            return;

        if (!_vote.Enabled)
            return;

        if (ev.LateJoin && !GiveOnLateJoin)
            return;

        if (!_givenThisRound.Add(ev.Player.UserId))
            return;

        if (!_proto.TryIndex<StartingLootPoolPrototype>(PoolId, out var pool))
            return;

        var valid = pool.Entries.Where(e => _proto.HasIndex<EntityPrototype>(e.Id)).ToList();
        if (valid.Count == 0)
            return;

        var bag = new List<StartingLootEntry>(valid);
        var results = new List<EntProtoId>();
        var rolls = Math.Min(pool.Rolls, bag.Count);

        for (var i = 0; i < rolls; i++)
        {
            var pick = PickWeighted(bag);
            bag.Remove(pick);
            results.Add(pick.Id);
            GiveItem(ev.Mob, pick.Id);
        }

        RaiseNetworkEvent(new StartingLootRolledEvent(results), ev.Player);
    }

    private StartingLootEntry PickWeighted(List<StartingLootEntry> list)
    {
        var total = list.Sum(e => Math.Max(0.0001f, e.Weight));
        var roll = _random.NextFloat() * total;

        foreach (var e in list)
        {
            roll -= Math.Max(0.0001f, e.Weight);
            if (roll <= 0f)
                return e;
        }

        return list[^1];
    }

    private void GiveItem(EntityUid mob, EntProtoId id)
    {
        var item = Spawn(id, Transform(mob).Coordinates);

        if (_inventory.TryGetSlotEntity(mob, "back", out var back)
            && TryComp<StorageComponent>(back, out var storage)
            && _storage.Insert(back.Value, item, out _, null, storage, false))
        {
            return;
        }

        _hands.PickupOrDrop(mob, item);
    }
}