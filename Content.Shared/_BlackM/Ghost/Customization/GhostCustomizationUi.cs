using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Ghost.Customization;

[Serializable, NetSerializable]
public enum GhostCustomizationUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class GhostCustomizationBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly List<string> Available;
    public readonly string Selected;

    public GhostCustomizationBoundUserInterfaceState(List<string> available, string selected)
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
