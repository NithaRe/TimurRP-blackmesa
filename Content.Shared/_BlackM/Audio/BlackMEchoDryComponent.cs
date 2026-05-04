using System.Collections.Generic;
using Robust.Shared.GameStates;

namespace Content.Shared._BlackM.Audio;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlackMEchoDryComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<string> DryPaths = [];
}
