using Robust.Shared.GameStates;

namespace Content.Shared._BlackM.HireTerminal;

[RegisterComponent, NetworkedComponent]
public sealed partial class HireTerminalComponent : Component
{
    [DataField]
    public string SlotId = "hireTerminal-slot";

    [DataField]
    public HashSet<string> LeaderJobIds = new() { "Captain" };

    [DataField]
    public int AutoApprovePlayerThreshold = 0;

    [DataField]
    public bool RequireOwner = true;

    [DataField]
    public int MaxLogEntries = 40;

    [DataField]
    public int MaxCommentLength = 120;
}
