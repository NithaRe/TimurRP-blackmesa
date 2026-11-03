using System.Numerics;
using Robust.Shared.Map;

namespace Content.Client._BlackM.Gib;

public interface IGibWorld
{
    bool Visible(MapId map, Vector2 pos);

    bool Blocked(MapId map, Vector2 pos);
}
