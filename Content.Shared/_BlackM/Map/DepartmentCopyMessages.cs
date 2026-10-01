using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Map;

[Serializable, NetSerializable]
public sealed class DepartmentCopyRequest(NetEntity grid, Vector2i[] tiles) : EntityEventArgs
{
    public const int MaxTiles = 16384;
    public readonly NetEntity Grid = grid;
    public readonly Vector2i[] Tiles = tiles;
}

[Serializable, NetSerializable]
public sealed class DepartmentCopyResult(string message, bool success = false) : EntityEventArgs
{
    public readonly string Message = message;
    public readonly bool Success = success;
}
