using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Power.EntitySystems;
using Content.Server.Research.Systems;
using Content.Server.Station.Systems;
using Content.Shared._BlackM.CrystalResearch;
using Content.Shared.Access.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Research.Components;
using Content.Shared.Roles.Jobs;
using Content.Shared.Zombies;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._BlackM.CrystalResearch;

public sealed class ZenResearchSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private StationJobsSystem _stationJobs = default!;
    [Dependency] private ResearchSystem _research = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;

    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawned);
        Subs.BuiEvents<ZenResearchConsoleComponent>(ZenCrystalUiKey.Console, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<ZenResearchPurchaseMessage>(OnPurchase);
        });
    }

    private void OnSpawned(PlayerSpawnCompleteEvent args)
    {
        if (args.JobId == null || !_stationJobs.GetJobs(args.Station).ContainsKey(args.JobId) ||
            !_mind.TryGetMind(args.Mob, out var mind, out _))
            return;

        EnsureComp<ZenResearchCrewMemberComponent>(mind).Station = args.Station;
        RefreshCrew();
    }

    public bool CanAward(EntityUid machine)
    {
        return _station.GetOwningStation(machine) is { } station &&
               _research.TryGetClientServer(machine, out var server, out _) &&
               _station.GetOwningStation(server.Value) == station && this.IsPowered(server.Value, EntityManager);
    }

    public bool TryAward(EntityUid machine)
    {
        if (!CanAward(machine) || _station.GetOwningStation(machine) is not { } station)
            return false;
        var research = EnsureComp<ZenResearchStationComponent>(station);
        research.Breakthroughs++;
        _chat.DispatchStationAnnouncement(machine, Loc.GetString("zen-research-breakthrough-announcement"),
            Loc.GetString("zen-research-announcement-sender"));
        UpdateConsoles();
        return true;
    }

    private void OnOpened(Entity<ZenResearchConsoleComponent> ent, ref BoundUIOpenedEvent args) => UpdateUi(ent);

    private void OnPurchase(Entity<ZenResearchConsoleComponent> ent, ref ZenResearchPurchaseMessage args)
    {
        if (!this.IsPowered(ent, EntityManager) || !Transform(ent).Anchored || !_access.IsAllowed(args.Actor, ent.Owner) ||
            _station.GetOwningStation(ent.Owner) is not { } station ||
            !_research.TryGetClientServer(ent, out var server, out var serverComp) ||
            _station.GetOwningStation(server.Value) != station || !this.IsPowered(server.Value, EntityManager))
            return;

        var research = EnsureComp<ZenResearchStationComponent>(station);
        if (args.Perk)
        {
            if (!_prototypes.TryIndex<ZenResearchPerkPrototype>(args.Id, out var perk))
                return;
            var level = research.Perks.GetValueOrDefault(perk.ID);
            if (level >= perk.Costs.Count || perk.Costs[level] <= 0 || research.Breakthroughs < perk.Costs[level])
                return;
            research.Breakthroughs -= perk.Costs[level];
            research.Perks[perk.ID] = level + 1;
            RefreshCrew();
            _chat.DispatchStationAnnouncement(ent, Loc.GetString("zen-research-perk-announcement",
                    ("perk", Loc.GetString(perk.Name)), ("level", level + 1)),
                Loc.GetString("zen-research-announcement-sender"));
        }
        else
        {
            if (!_prototypes.TryIndex<ZenResearchSupplyPrototype>(args.Id, out var supply) || supply.Cost <= 0 ||
                serverComp.Points < supply.Cost || research.Deliveries.Count >= 5 ||
                research.Purchased.GetValueOrDefault(supply.ID) >= supply.Stock)
                return;
            _research.ModifyServerPoints(server.Value, -supply.Cost, serverComp);
            research.Purchased[supply.ID] = research.Purchased.GetValueOrDefault(supply.ID) + 1;
            research.Deliveries.Add(new ZenResearchDelivery
            {
                Supply = supply.ID,
                Destination = ent.Owner,
                Remaining = (float) supply.DeliveryTime.TotalSeconds,
            });
        }
        UpdateConsoles();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _accumulator += frameTime;
        if (_accumulator < 1f)
            return;
        var elapsed = _accumulator;
        _accumulator = 0f;

        var query = EntityQueryEnumerator<ZenResearchStationComponent>();
        while (query.MoveNext(out var station, out var research))
        {
            for (var i = research.Deliveries.Count - 1; i >= 0; i--)
            {
                var delivery = research.Deliveries[i];
                delivery.Remaining = Math.Max(0f, delivery.Remaining - elapsed);
                if (delivery.Remaining > 0f || !_prototypes.TryIndex(delivery.Supply, out var supply))
                    continue;

                // A destroyed receiver does not delete paid orders. Another station terminal can receive them.
                if (!IsReceiver(delivery.Destination, station))
                {
                    var receivers = EntityQueryEnumerator<ZenResearchConsoleComponent>();
                    while (receivers.MoveNext(out var receiver, out _))
                    {
                        if (!IsReceiver(receiver, station))
                            continue;
                        delivery.Destination = receiver;
                        break;
                    }
                }
                if (!IsReceiver(delivery.Destination, station))
                    continue;
                Spawn(supply.Product, Transform(delivery.Destination).Coordinates);
                research.Deliveries.RemoveAt(i);
            }
        }
        RefreshCrew();
        UpdateConsoles();
    }

    private bool IsReceiver(EntityUid uid, EntityUid station) => !TerminatingOrDeleted(uid) &&
        HasComp<ZenResearchConsoleComponent>(uid) && _station.GetOwningStation(uid) == station &&
        Transform(uid).Anchored && this.IsPowered(uid, EntityManager);

    private void RefreshCrew()
    {
        var query = EntityQueryEnumerator<MindContainerComponent, HumanoidAppearanceComponent>();
        while (query.MoveNext(out var uid, out var container, out _))
        {
            var eligible = container.Mind is { } mind &&
                           TryComp<ZenResearchCrewMemberComponent>(mind, out var membership) &&
                           TryComp<ZenResearchStationComponent>(membership.Station, out var stationResearch) &&
                           _jobs.MindTryGetJobId(mind, out var job) && job != null &&
                           _stationJobs.GetJobs(membership.Station).ContainsKey(job.Value) &&
                           !HasComp<GhostComponent>(uid) && !HasComp<ZombieComponent>(uid);
            if (!eligible)
            {
                RemCompDeferred<ZenResearchCrewBonusComponent>(uid);
                continue;
            }

            // Resolve again after the eligibility check to keep the station identity explicit.
            var member = Comp<ZenResearchCrewMemberComponent>(container.Mind!.Value);
            var data = Comp<ZenResearchStationComponent>(member.Station);
            float speed = 0f, pull = 0f, resistance = 0f;
            foreach (var (id, level) in data.Perks)
            {
                if (!_prototypes.TryIndex<ZenResearchPerkPrototype>(id, out var perk))
                    continue;
                speed += perk.SpeedPerLevel * level;
                pull += perk.PullSpeedPerLevel * level;
                resistance += perk.InfectionResistancePerLevel * level;
            }
            var bonus = EnsureComp<ZenResearchCrewBonusComponent>(uid);
            if (MathHelper.CloseTo(bonus.Speed, speed) && MathHelper.CloseTo(bonus.PullSpeed, pull) &&
                MathHelper.CloseTo(bonus.InfectionResistance, resistance))
                continue;
            bonus.Speed = speed;
            bonus.PullSpeed = pull;
            bonus.InfectionResistance = Math.Clamp(resistance, 0f, 0.9f);
            Dirty(uid, bonus);
            _movement.RefreshMovementSpeedModifiers(uid);
        }

        var oldBonuses = EntityQueryEnumerator<ZenResearchCrewBonusComponent>();
        while (oldBonuses.MoveNext(out var uid, out _))
        {
            if (!HasComp<HumanoidAppearanceComponent>(uid) || !HasComp<MindContainerComponent>(uid))
                RemCompDeferred<ZenResearchCrewBonusComponent>(uid);
        }
    }

    private void UpdateConsoles()
    {
        var query = EntityQueryEnumerator<ZenResearchConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
            UpdateUi((uid, console));
    }

    private void UpdateUi(Entity<ZenResearchConsoleComponent> ent)
    {
        var state = new ZenResearchConsoleState
        {
            Powered = this.IsPowered(ent, EntityManager) && Transform(ent).Anchored,
        };
        if (_station.GetOwningStation(ent.Owner) is { } station)
        {
            var research = EnsureComp<ZenResearchStationComponent>(station);
            state.Breakthroughs = research.Breakthroughs;
            if (_research.TryGetClientServer(ent, out var server, out var serverComp) &&
                _station.GetOwningStation(server.Value) == station && this.IsPowered(server.Value, EntityManager))
            {
                state.Connected = true;
                state.Points = serverComp.Points;
            }
            foreach (var perk in _prototypes.EnumeratePrototypes<ZenResearchPerkPrototype>().OrderBy(p => p.ID))
            {
                var level = research.Perks.GetValueOrDefault(perk.ID);
                state.Perks.Add(new ZenResearchPerkEntry(perk.ID, Loc.GetString(perk.Name), Loc.GetString(perk.Description),
                    level, perk.Costs.Count, level < perk.Costs.Count ? perk.Costs[level] : 0));
            }
            foreach (var supply in _prototypes.EnumeratePrototypes<ZenResearchSupplyPrototype>().OrderBy(p => p.ID))
                state.Supplies.Add(new ZenResearchSupplyEntry(supply.ID, Loc.GetString(supply.Name), supply.Product.Id,
                    supply.Cost, Math.Max(0, supply.Stock - research.Purchased.GetValueOrDefault(supply.ID)),
                    (int) Math.Ceiling(supply.DeliveryTime.TotalSeconds)));
            foreach (var delivery in research.Deliveries)
            {
                if (_prototypes.TryIndex(delivery.Supply, out var supply))
                    state.Deliveries.Add(Loc.GetString("zen-research-delivery", ("name", Loc.GetString(supply.Name)),
                        ("seconds", (int) Math.Ceiling(delivery.Remaining))));
            }
        }
        _ui.SetUiState(ent.Owner, ZenCrystalUiKey.Console, state);
    }
}
