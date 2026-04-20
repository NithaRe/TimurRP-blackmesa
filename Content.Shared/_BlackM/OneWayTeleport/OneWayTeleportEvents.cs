using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.OneWayTeleport;

[Serializable, NetSerializable]
public sealed partial class OneWayTeleportDoAfterEvent : SimpleDoAfterEvent { }
