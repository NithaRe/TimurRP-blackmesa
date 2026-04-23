namespace Content.Server._BlackM.SpawnMarkers;

[RegisterComponent]
public sealed partial class MobSpawnMarkerComponent : Component
{
    [DataField(required: true)]
    public string Prototype = string.Empty;

    [DataField]
    public int Count = 1;

    [DataField]
    public float RespawnDelay = 60f;

    [DataField]
    public TimeSpan NextSpawnTime;
}

[RegisterComponent]
public sealed partial class MobSpawnRadiusMarkerComponent : Component
{
    [DataField(required: true)]
    public string Prototype = string.Empty;

    [DataField]
    public int Count = 1;

    [DataField]
    public float Radius = 3f;

    [DataField]
    public float RespawnDelay = 60f;

    [DataField]
    public TimeSpan NextSpawnTime;
}

[RegisterComponent]
public sealed partial class ItemSpawnMarkerComponent : Component
{
    [DataField(required: true)]
    public List<string> Prototypes = new();

    [DataField]
    public int Count = 1;
}

[RegisterComponent]
public sealed partial class ItemSpawnRadiusMarkerComponent : Component
{
    [DataField(required: true)]
    public List<string> Prototypes = new();

    [DataField]
    public int Count = 1;

    [DataField]
    public float Radius = 3f;
}
