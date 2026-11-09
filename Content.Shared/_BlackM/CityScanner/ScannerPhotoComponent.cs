using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Enums;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.CityScanner;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ScannerPhotoComponent : Component
{
    [DataField, AutoNetworkedField]
    public string Subject = string.Empty;

    [DataField, AutoNetworkedField]
    public ScannerSnapshot? Snapshot;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class ScannerSnapshot
{
    // Fallback for non-humanoid subjects.
    [DataField]
    public string? Prototype;

    [DataField]
    public ProtoId<SpeciesPrototype>? Species;

    [DataField]
    public Sex Sex = Sex.Male;

    [DataField]
    public Gender Gender;

    [DataField]
    public int Age = 18;

    [DataField]
    public Color SkinColor = Color.White;

    [DataField]
    public Color EyeColor = Color.Brown;

    [DataField]
    public float Height = 1f;

    [DataField]
    public float Width = 1f;

    [DataField]
    public MarkingSet Markings = new();

    [DataField]
    public Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo> CustomBaseLayers = new();

    [DataField]
    public Dictionary<string, string> Equipment = new();
}

[Serializable, NetSerializable]
public enum ScannerPhotoUiKey : byte
{
    Key,
}
