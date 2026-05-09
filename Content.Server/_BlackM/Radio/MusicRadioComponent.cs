using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server._BlackM.Radio;

/// <summary>
///   Радио, которое играет музыку в loop.
///   Зачем? просто потому что я захотел.
/// </summary>
[RegisterComponent]
public sealed partial class MusicRadioComponent : Component
{
    [DataField(required: true)]
    public SoundSpecifier Sound = default!;

    [DataField]
    public float Volume = -6f;

    [DataField]
    public float Range = 10f;

    public EntityUid? Stream;

    public bool Playing;
}
