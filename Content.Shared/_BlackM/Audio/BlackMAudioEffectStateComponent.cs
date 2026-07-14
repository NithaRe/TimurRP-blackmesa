using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._BlackM.Audio;

[RegisterComponent]
public sealed partial class BlackMAudioEffectStateComponent : Component
{
    [ViewVariables]
    public ProtoId<AudioPresetPrototype>? BasePreset;

    [ViewVariables]
    public ProtoId<AudioPresetPrototype>? OverridePreset;

    [ViewVariables]
    public ProtoId<AudioPresetPrototype>? AppliedPreset;

    [ViewVariables]
    public bool NeedsReconcile = true;
}
