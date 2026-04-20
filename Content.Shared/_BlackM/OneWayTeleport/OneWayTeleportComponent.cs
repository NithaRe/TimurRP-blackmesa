namespace Content.Shared._BlackM.OneWayTeleport;

[RegisterComponent]
public sealed partial class OneWayTeleportComponent : Component
{
    [DataField]
    public float Delay = 10f;

    [DataField]
    public string DestinationId = "default";

    /// <summary>Радиус триггер-зоны.</summary>
    [DataField]
    public float Range = 1.5f;

    [DataField]
    public HashSet<EntityUid> ActiveDoAfters = new();
}

[RegisterComponent]
public sealed partial class OneWayTeleportDestinationComponent : Component
{
    /// <summary>ID группы — должен совпадать с OneWayTeleportComponent.DestinationId.</summary>
    [DataField]
    public string DestinationId = "default";
}

/// <summary>
/// Вешается на игрока после телепортации запрещает повторный телепорт через этот же DestinationId хз зачем надо оно или не надо но пускай будет.
/// </summary>
[RegisterComponent]
public sealed partial class OneWayTeleportUsedComponent : Component
{
    [DataField]
    public HashSet<string> UsedIds = new();
}
