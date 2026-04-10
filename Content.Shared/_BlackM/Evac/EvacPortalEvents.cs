using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Evac;

[Serializable, NetSerializable]
public sealed partial class EvacPortalDoAfterEvent : SimpleDoAfterEvent { }
