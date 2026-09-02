using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Ghost.Customization;

[Serializable, NetSerializable]
public enum GhostCustomizationUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class GhostCustomizationOptionState
{
    public readonly string Id;
    public readonly string Name;
    public readonly bool Locked;
    public readonly string? LockReason;

    public GhostCustomizationOptionState(string id, string name, bool locked, string? lockReason)
    {
        Id = id;
        Name = name;
        Locked = locked;
        LockReason = lockReason;
    }
}

[Serializable, NetSerializable]
public sealed class GhostCustomizationBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly List<GhostCustomizationOptionState> Available;
    public readonly string Selected;

    public GhostCustomizationBoundUserInterfaceState(List<GhostCustomizationOptionState> available, string selected)
    {
        Available = available;
        Selected = selected;
    }
}

[Serializable, NetSerializable]
public sealed class GhostSpriteSelectedMessage : BoundUserInterfaceMessage
{
    public readonly string SpriteId;

    public GhostSpriteSelectedMessage(string spriteId)
    {
        SpriteId = spriteId;
    }
}

public sealed partial class GhostCustomizationActionEvent : InstantActionEvent
{
}
