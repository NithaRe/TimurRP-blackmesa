// SPDX-FileCopyrightText: 2026 NithaRe
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Players.PlayTimeTracking;
using Content.Shared.GameTicking;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Roles;
using Content.Shared.Station;
using Robust.Shared.Prototypes;

namespace Content.Server._BlackM.Guidebook;

public sealed class NewPlayerGuideSystem : EntitySystem
{
    [Dependency] private readonly PlayTimeTrackingManager _playTime = default!;
    [Dependency] private readonly SharedStationSpawningSystem _stationSpawning = default!;

    private static readonly TimeSpan NewPlayerThreshold = TimeSpan.FromHours(10);
    private static readonly ProtoId<StartingGearPrototype> NewPlayerGuideGear = "BlackMNewPlayerGuideGear";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        TryGiveNewPlayerGuide(args);
    }

    public bool TryGiveNewPlayerGuide(PlayerSpawnCompleteEvent args)
    {
        if (!CanGiveNewPlayerGuide(args))
            return false;

        _stationSpawning.EquipStartingGear(args.Mob, NewPlayerGuideGear);
        return true;
    }

    public bool CanGiveNewPlayerGuide(PlayerSpawnCompleteEvent args)
    {
        if (args.JobId == null || !_playTime.TryGetTrackerTimes(args.Player, out var playTimes))
            return false;

        playTimes.TryGetValue(PlayTimeTrackingShared.TrackerOverall, out var overallPlayTime);
        return overallPlayTime < NewPlayerThreshold;
    }
}
