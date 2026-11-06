namespace Content.Shared._BlackM.Underground;

[RegisterComponent]
public sealed partial class UndergroundConsoleComponent : Component
{
    [DataField]
    public string LinkId = "main";

    [DataField]
    public bool IsReturn;

    // Runtime link set by the expedition coordinator, never by player input.
    public EntityUid? MainConsole;
}

[RegisterComponent]
public sealed partial class UndergroundBoardingZoneComponent : Component
{
    [DataField]
    public string LinkId = "main";

    [DataField]
    public float HalfWidth = 1.5f;
}
