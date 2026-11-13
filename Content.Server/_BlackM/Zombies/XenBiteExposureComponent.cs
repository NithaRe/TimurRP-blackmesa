namespace Content.Server.Zombies;

[RegisterComponent]
public sealed partial class XenBiteExposureComponent : Component
{
    [DataField]
    public int Bites;

    [ViewVariables]
    public TimeSpan ResetAt;
}
