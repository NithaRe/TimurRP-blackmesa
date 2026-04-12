// Originally authored by mirrorcult for EphemeralSpace (https://github.com/EphemeralSpace/ephemeral-space)
// Ported to this project with modifications under the same license.

using Robust.Shared.GameStates;

namespace Content.Shared._BlackM.Viewcone;

/// <summary>
/// ⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⡠⠀⠀⠀⡀⢄⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
///           THE CONE MAN APPROACHES
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BMViewconeComponent : Component
{
    [DataField, AutoNetworkedField]
    public float ConeAngle = 270f;

    [DataField, AutoNetworkedField]
    public float ConeFeather = 10f;

    [DataField, AutoNetworkedField]
    public float ConeIgnoreRadius = 0.85f;

    [DataField, AutoNetworkedField]
    public float ConeIgnoreFeather = 0.25f;

    // Clientside, used for lerping view angle
    // and keeping it consistent across all overlays
    public Angle ViewAngle;
    public Angle? DesiredViewAngle = null;
    public Angle LastMouseRotationAngle;
    public Angle LastWorldRotationAngle;
}
