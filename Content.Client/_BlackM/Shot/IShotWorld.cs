using System.Numerics;
using Robust.Shared.Map;

namespace Content.Client._BlackM.Shot;

public interface IShotWorld
{
    bool Visible(MapId map, Vector2 pos);
    bool Blocked(MapId map, Vector2 pos);
}
