using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Prototypes;
using Robust.Shared.ViewVariables;

namespace Content.Shared._BlackM.Radio;

[NetworkedComponent, RegisterComponent, AutoGenerateComponentState(true)]
public sealed partial class MusicRadioComponent : Component
{
    [DataField(required: true)]
    public List<MusicRadioTrack> Tracks = new();

    [DataField, AutoNetworkedField]
    public int CurrentTrack;

    [DataField]
    public float Volume = -6f;

    [DataField]
    public float Range = 10f;

    [DataField, AutoNetworkedField]
    public bool Playing;

    [DataField, AutoNetworkedField]
    public EntityUid? AudioStream;

    [DataField]
    public bool AutoAdvance = true;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class MusicRadioTrack
{
    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField(required: true)]
    public SoundSpecifier Sound = default!;
}
