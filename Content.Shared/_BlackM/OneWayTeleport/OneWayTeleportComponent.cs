namespace Content.Shared._BlackM.OneWayTeleport;

[RegisterComponent]
public sealed partial class OneWayTeleportComponent : Component
{
    [DataField]
    public float Delay = 10f;

    [DataField]
    public string DestinationId = "default";

    [DataField]
    public float Range = 1.5f;

    [DataField]
    public bool Enabled = true;

    [DataField]
    public string WarningMessage = "one-way-teleport-warning-default";

    [DataField]
    public HashSet<EntityUid> ActiveDoAfters = new();
}

[RegisterComponent]
public sealed partial class OneWayTeleportDestinationComponent : Component
{
    [DataField]
    public string DestinationId = "default";
}

[RegisterComponent]
public sealed partial class OneWayTeleportUsedComponent : Component
{
    [DataField]
    public HashSet<string> UsedIds = new();
}
