using Content.Server.Chat.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Research.Prototypes;

namespace Content.Server.Research.Systems;

public sealed partial class ResearchSystem
{
    [Dependency] private readonly StationSystem _rewardStations = default!;
    [Dependency] private readonly ChatSystem _rewardChat = default!;

    private void AnnounceResearchReward(EntityUid client, TechnologyPrototype technology)
    {
        if (technology.ResearchRewardAnnouncement is not { } message ||
            _rewardStations.GetOwningStation(client) is not { } station)
            return;

        _rewardChat.DispatchStationAnnouncement(station, Loc.GetString(message), Name(client), playDefaultSound: true);
    }
}
