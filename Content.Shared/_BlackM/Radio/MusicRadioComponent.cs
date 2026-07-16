using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Prototypes;
using Robust.Shared.ViewVariables;

namespace Content.Shared._BlackM.Radio;

[RegisterComponent, AutoGenerateComponentState]
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

    [ViewVariables]
    public EntityUid? Stream;

    [DataField]
    public float WatchdogInterval = 1f;

    [ViewVariables]
    public float WatchdogAccumulator;

    [DataField]
    public bool AutoAdvance = true;

    [ViewVariables]
    public TimeSpan? TrackEndTime;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class MusicRadioTrack
{
    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField(required: true)]
    public SoundSpecifier Sound = default!;
}
