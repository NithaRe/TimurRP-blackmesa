using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Gib;

[Serializable, NetSerializable]
public sealed class GibBurstEvent : EntityEventArgs
{
    public MapId Map;
    public Vector2 Position;
    public float Strength;
    public Color Color;

    public GibBurstEvent(MapId map, Vector2 position, float strength, Color color)
    {
        Map = map;
        Position = position;
        Strength = strength;
        Color = color;
    }
}
