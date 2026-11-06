using Robust.Shared.GameStates;

namespace Content.Shared._BlackM.SpeechBarks;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SpeechBarksComponent : Component
{
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public BarkData Data = new();
}
