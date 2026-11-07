using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.ZenCatalyst;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ZenCatalystStatusComponent : Component
{
    [AutoNetworkedField]
    public List<ZenCatalystWaveStatus> Catalysts = new();
}

[Serializable, NetSerializable]
public readonly record struct ZenCatalystWaveStatus(
    TimeSpan SpawnInterval,
    int SpawnAmount,
    TimeSpan NextSpawnTime);
