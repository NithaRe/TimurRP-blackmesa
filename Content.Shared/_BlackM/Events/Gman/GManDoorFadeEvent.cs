using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Events.Gman;

[Serializable, NetSerializable]
public sealed class GManDoorFadeEvent : EntityEventArgs
{
    public NetEntity Door;
    public float FadeTime;
}
