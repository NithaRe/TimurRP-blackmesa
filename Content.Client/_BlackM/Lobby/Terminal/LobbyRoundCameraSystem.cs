using Content.Shared._BlackM.Lobby;
using Robust.Client.GameObjects;

namespace Content.Client._BlackM.Lobby.Terminal;


public sealed class LobbyRoundCameraSystem : EntitySystem
{
    public NetEntity? Camera { get; private set; }

    public event Action<NetEntity?>? CameraChanged;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<LobbyRoundCameraEvent>(OnCameraChanged);
    }

    private void OnCameraChanged(LobbyRoundCameraEvent ev)
    {
        Camera = ev.Camera;
        CameraChanged?.Invoke(Camera);
    }
}
