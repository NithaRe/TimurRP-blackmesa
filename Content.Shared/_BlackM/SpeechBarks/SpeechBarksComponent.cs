using Robust.Shared.GameStates;

namespace Content.Shared._BlackM.SpeechBarks;

[RegisterComponent, NetworkedComponent]
public sealed partial class SpeechBarksComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public BarkData Data = new();
}
