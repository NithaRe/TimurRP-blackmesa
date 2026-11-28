using System.Linq;
using System.Numerics;
using System.Text;
using Content.Server.Cargo.Systems;
using Content.Server.Pinpointer;
using Content.Server.StationEvents.Events;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._BlackM.ZenRift;

public sealed partial class ZenRiftRuleSystem : StationEventSystem<ZenRiftRuleComponent>
{
    [Dependency] private ZenRiftSystem _rift = default!;
    [Dependency] private CargoSystem _cargo = default!;
    [Dependency] private NavMapSystem _navMap = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    private const string KeyStart = "start";
    private const string KeyBreach = "breach";
    private const string KeyCalm = "calm";
    private const string KeyResult = "result";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZenRiftRuleComponent, ZenRiftClosedEvent>(OnRiftClosed);
        SubscribeLocalEvent<ZenRiftRuleComponent, ZenRiftMobSpawnedEvent>(OnMobSpawned);
        SubscribeLocalEvent<ZenRiftRuleComponent, ZenRiftMobNeutralizedEvent>(OnMobNeutralized);
        SubscribeLocalEvent<ZenRiftRuleComponent, ZenRiftActivatedEvent>(OnRiftActivated);
    }

    protected override void Started(EntityUid uid, ZenRiftRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        if (!TryPickStation(comp, out var station))
        {
            Sawmill.Warning("ZenRift: no station found, ending rule.");
            ForceEndSelf(uid, gameRule);
            return;
        }

        comp.Station = station;

        var count = RobustRandom.Next(comp.MinRifts, comp.MaxRifts + 1);
        var placed = new List<EntityCoordinates>();

        for (var i = 0; i < count; i++)
        {
            if (!TryFindRiftCoordinates(station, comp, placed, out var coords))
                continue;

            placed.Add(coords);

            var rift = Spawn(comp.RiftPrototype.Id, coords);
            var budget = RobustRandom.Next(comp.MinMobsPerRift, comp.MaxMobsPerRift + 1);

            TimeSpan? openDelay = null;
            if (TryComp<ZenRiftComponent>(rift, out var riftComp))
            {
                var baseDelay = comp.OpenDelay ?? riftComp.OpenDuration;
                var stagger = TimeSpan.FromSeconds(RobustRandom.NextDouble(0, comp.OpenStagger.TotalSeconds));
                openDelay = baseDelay + stagger;
            }

            _rift.Configure(rift, uid, budget, openDelay);
            comp.Rifts.Add(rift);
        }

        if (comp.Rifts.Count == 0)
        {
            Sawmill.Warning("ZenRift: failed to place any rift, ending rule.");
            ForceEndSelf(uid, gameRule);
            return;
        }

        comp.Placed = true;
        comp.MaxEndAt = Timing.CurTime + comp.MaxDuration;

        var locations = comp.AnnounceLocations ? BuildLocationList(comp) : string.Empty;
        AnnounceOnce(comp, KeyStart, Loc.GetString(comp.StartAnnouncement, ("locations", locations)),
            comp.StartSound, comp.InfoColor);
    }

    protected override void ActiveTick(EntityUid uid, ZenRiftRuleComponent comp, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, comp, gameRule, frameTime);

        if (comp.Finished || !comp.Placed)
            return;

        comp.Rifts.RemoveWhere(r => TerminatingOrDeleted(r));
        comp.Mobs.RemoveWhere(m => !IsStationThreat(comp, m));

        var now = Timing.CurTime;

        if (now >= comp.MaxEndAt)
        {
            Finish(uid, comp);
            return;
        }

        if (comp.Rifts.Count > 0)
            return;

        if (comp.Mobs.Count == 0)
        {
            Finish(uid, comp);
            return;
        }

        if (comp.SweepEndAt == null)
        {
            comp.SweepEndAt = now + comp.SweepDuration;
            AnnounceOnce(comp, KeyCalm, Loc.GetString(comp.CalmAnnouncement), comp.CalmSound, comp.InfoColor);
        }

        if (now >= comp.SweepEndAt)
            Finish(uid, comp);
    }

    protected override void Ended(EntityUid uid, ZenRiftRuleComponent comp, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, comp, gameRule, args);

        if (!comp.Finished && comp.Placed && comp.PayOnForcedEnd)
            Finish(uid, comp, endRule: false);

        foreach (var rift in comp.Rifts.ToArray())
        {
            if (TryComp<ZenRiftComponent>(rift, out var riftComp))
            {
                riftComp.Rule = null;
                _rift.ForceClose(rift, riftComp);
            }
        }

        comp.Rifts.Clear();
        comp.Mobs.Clear();
    }

    private void OnRiftActivated(Entity<ZenRiftRuleComponent> ent, ref ZenRiftActivatedEvent args)
    {
        if (ent.Comp.Finished)
            return;

        AnnounceOnce(ent.Comp, KeyBreach, Loc.GetString(ent.Comp.BreachAnnouncement),
            ent.Comp.BreachSound, ent.Comp.BreachColor);
    }

    private void OnRiftClosed(Entity<ZenRiftRuleComponent> ent, ref ZenRiftClosedEvent args)
    {
        ent.Comp.Rifts.Remove(args.Rift);
    }

    private void OnMobSpawned(Entity<ZenRiftRuleComponent> ent, ref ZenRiftMobSpawnedEvent args)
    {
        ent.Comp.Mobs.Add(args.Mob);
        ent.Comp.TotalSpawned++;
    }

    private void OnMobNeutralized(Entity<ZenRiftRuleComponent> ent, ref ZenRiftMobNeutralizedEvent args)
    {
        if (ent.Comp.Finished)
            return;

        ent.Comp.Neutralized++;
        ent.Comp.Mobs.Remove(args.Mob);
    }

    private bool IsStationThreat(ZenRiftRuleComponent comp, EntityUid mob)
    {
        if (!_rift.IsThreat(mob))
            return false;

        return comp.Station == null || StationSystem.GetOwningStation(mob) == comp.Station;
    }

    private void Finish(EntityUid uid, ZenRiftRuleComponent comp, bool endRule = true)
    {
        if (comp.Finished)
            return;

        comp.Finished = true;
        comp.Mobs.RemoveWhere(m => !IsStationThreat(comp, m));

        var cleared = comp.Mobs.Count == 0;

        var reward = comp.WaveReward + comp.Neutralized * comp.KillBonus;
        if (cleared)
            reward += comp.ClearBonus;

        if (comp.MaxReward > 0)
            reward = Math.Min(reward, comp.MaxReward);

        var paid = PayReward(comp, reward);

        if (cleared)
        {
            AnnounceOnce(comp, KeyResult, Loc.GetString(comp.ClearedAnnouncement, ("reward", paid)),
                comp.ClearedSound, comp.ResultColor);
        }
        else
        {
            AnnounceOnce(comp, KeyResult, Loc.GetString(comp.SurvivedAnnouncement, ("reward", paid)),
                comp.SurvivedSound, comp.ResultColor);
        }

        Sawmill.Info($"Zen rift wave finished: spawned {comp.TotalSpawned}, neutralized {comp.Neutralized}, " +
                     $"left {comp.Mobs.Count}, paid {paid}.");

        if (endRule)
            GameTicker.EndGameRule(uid);
    }

    private int PayReward(ZenRiftRuleComponent comp, int amount)
    {
        if (amount <= 0 || comp.Station is not { } station || !TryComp<StationBankAccountComponent>(station, out var bank))
            return 0;

        IReadOnlyDictionary<ProtoId<CargoAccountPrototype>, int> accounts = bank.Accounts;
        var account = comp.RewardAccount;
        if (!accounts.ContainsKey(account))
            account = bank.PrimaryAccount;

        _cargo.UpdateBankAccount((station, bank), amount, account);
        Sawmill.Info($"Zen rift reward: {amount} credited to {account} on station {ToPrettyString(station)}");
        return amount;
    }

    #region Placement

    private bool TryPickStation(ZenRiftRuleComponent comp, out EntityUid station)
    {
        station = default;

        if (!string.IsNullOrEmpty(comp.TargetStationName)
            && TryGetRandomStation(out var named, s => MetaData(s).EntityName.Contains(comp.TargetStationName, StringComparison.OrdinalIgnoreCase))
            && HasComp<StationDataComponent>(named))
        {
            station = named.Value;
            return true;
        }

        if (TryGetRandomStation(out var random) && HasComp<StationDataComponent>(random))
        {
            station = random.Value;
            return true;
        }

        return false;
    }

    private bool TryFindRiftCoordinates(EntityUid station, ZenRiftRuleComponent comp, List<EntityCoordinates> placed, out EntityCoordinates coords)
    {
        coords = default;

        if (!TryComp<StationDataComponent>(station, out var data))
            return false;

        var grids = data.Grids.ToList();
        if (grids.Count == 0)
            return false;

        for (var attempt = 0; attempt < comp.MaxPlacementAttempts; attempt++)
        {
            var gridUid = RobustRandom.Pick(grids);
            if (!TryComp<MapGridComponent>(gridUid, out var grid))
                continue;

            var bounds = grid.LocalAABB;
            if (bounds.IsEmpty())
                continue;

            var randomLocal = new Vector2(
                RobustRandom.NextFloat(bounds.Left, bounds.Right),
                RobustRandom.NextFloat(bounds.Bottom, bounds.Top));

            var tile = _map.LocalToTile(gridUid, grid, new EntityCoordinates(gridUid, randomLocal));
            if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
                continue;

            var candidate = _map.GridTileToLocal(gridUid, grid, tile);
            if (_lookup.AnyEntitiesIntersecting(candidate, LookupFlags.Static))
                continue;

            if (!HasFreeNeighbour(gridUid, grid, tile))
                continue;

            var mapCoords = _transform.ToMapCoordinates(candidate);

            var tooClose = false;
            foreach (var other in placed)
            {
                var otherMap = _transform.ToMapCoordinates(other);
                if (otherMap.MapId == mapCoords.MapId
                    && (otherMap.Position - mapCoords.Position).Length() < comp.MinRiftDistance)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose)
                continue;

            if (_lookup.GetEntitiesInRange<ActorComponent>(mapCoords, comp.MinPlayerDistance).Count > 0)
                continue;

            coords = candidate;
            return true;
        }

        return false;
    }

    private bool HasFreeNeighbour(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var free = 0;
        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                var n = tile + new Vector2i(dx, dy);
                if (!_map.TryGetTileRef(gridUid, grid, n, out var nRef) || nRef.Tile.IsEmpty)
                    continue;

                if (_lookup.AnyEntitiesIntersecting(_map.GridTileToLocal(gridUid, grid, n), LookupFlags.Static))
                    continue;

                free++;
            }
        }

        return free >= 3;
    }

    #endregion

    #region Announcements

    private string GetLocation(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid))
            return Loc.GetString("zen-rift-location-unknown");

        return FormattedMessage.RemoveMarkupPermissive(_navMap.GetNearestBeaconString((uid, Transform(uid))));
    }

    private string BuildLocationList(ZenRiftRuleComponent comp)
    {
        var seen = new HashSet<string>();
        var sb = new StringBuilder();
        foreach (var rift in comp.Rifts)
        {
            if (TerminatingOrDeleted(rift))
                continue;

            var location = GetLocation(rift);
            if (!seen.Add(location))
                continue;

            sb.Append('\n');
            sb.Append(Loc.GetString("zen-rift-list-entry", ("location", location)));
        }

        return sb.ToString();
    }

    private void AnnounceOnce(ZenRiftRuleComponent comp, string key, string message, SoundSpecifier? sound, Color color)
    {
        if (!comp.Announced.Add(key))
            return;

        var filter = Filter.Empty().AddWhere(GameTicker.UserHasJoinedGame);
        ChatSystem.DispatchFilteredAnnouncement(filter, message,
            sender: Loc.GetString(comp.Sender),
            playSound: sound != null,
            announcementSound: sound,
            colorOverride: color);
    }

    #endregion
}
