using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Lobby;


[Serializable, NetSerializable]
public sealed class LobbyRoundCameraEvent(NetEntity? camera) : EntityEventArgs
{
    public NetEntity? Camera { get; } = camera;
}
