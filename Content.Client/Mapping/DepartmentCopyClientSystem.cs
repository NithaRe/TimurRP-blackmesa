using Content.Shared._BlackM.Map;

namespace Content.Client.Mapping;

public sealed class DepartmentCopyClientSystem : EntitySystem
{
    public event Action<DepartmentCopyResult>? Result;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<DepartmentCopyResult>(msg => Result?.Invoke(msg));
    }

    public void Copy(EntityUid grid, Vector2i[] tiles)
        => RaiseNetworkEvent(new DepartmentCopyRequest(GetNetEntity(grid), tiles));
}
