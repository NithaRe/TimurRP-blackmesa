using Content.Shared.Atmos;
using Robust.Shared.GameStates;

namespace Content.Shared._BlackM.Environment;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NoSpaceComponent : Component
{
    [DataField]
    public bool Gravity = true;

    [DataField]
    public bool Breathable = true;

    [DataField]
    public float Oxygen = Atmospherics.OxygenMolesStandard;

    [DataField]
    public float Nitrogen = Atmospherics.NitrogenMolesStandard;

    [DataField]
    public float Temperature = Atmospherics.T20C;

    [DataField]
    public bool FillVacuum = true;

    [DataField]
    public float FillBelowPressure = 80f;

    [DataField]
    public bool SilenceWind = true;

    [DataField, AutoNetworkedField]
    public bool NoSpaceAmbience = true;

    [DataField]
    public Color? MapLight;

    [DataField]
    public string? Parallax;

    [ViewVariables]
    public bool Applied;

    [ViewVariables]
    public int FillPassesLeft;

    [ViewVariables]
    public TimeSpan NextFill;
}
