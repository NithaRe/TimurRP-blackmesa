using Content.Shared._BlackM.Underground;

namespace Content.Server._BlackM.Underground;

[RegisterComponent]
public sealed partial class UndergroundMissionTargetComponent : Component
{
    public EntityUid Console;
    public long Generation;
    public bool Counted;
}

[RegisterComponent]
public sealed partial class UndergroundMissionSampleComponent : Component
{
    public UndergroundMission Mission;
    public EntityUid Console;
    public long Generation;
}
