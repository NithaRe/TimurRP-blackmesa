using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.NameTag;

[Serializable, NetSerializable]
public enum NameTagAuraStyle : byte
{
    None,
    Rune,  
    Flame,  
    Orbit, 
}
