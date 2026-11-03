using Robust.Shared.GameStates;

namespace Content.Shared._BlackM.Gib;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GibTrailComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color Color = Color.FromHex("#7d0a0d");
}
