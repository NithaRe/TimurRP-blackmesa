using Robust.Shared.GameStates;

namespace Content.Shared._BlackM.Gib;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GibFadeComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan StartTime;

    [DataField, AutoNetworkedField]
    public float Lifetime = 3f;

    [DataField, AutoNetworkedField]
    public float FadeDuration = 1.2f;

    [DataField, AutoNetworkedField]
    public float EndScale = 0.4f;
}
