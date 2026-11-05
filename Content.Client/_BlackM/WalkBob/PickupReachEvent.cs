using System.Numerics;
using Robust.Shared.Map;

namespace Content.Client._BlackM.WalkBob;

public sealed class PickupReachEvent : EntityEventArgs
{
    public readonly MapId MapId;
    public readonly Vector2 From;
    public readonly Vector2 To;

    public PickupReachEvent(MapId mapId, Vector2 from, Vector2 to)
    {
        MapId = mapId;
        From = from;
        To = to;
    }
}
