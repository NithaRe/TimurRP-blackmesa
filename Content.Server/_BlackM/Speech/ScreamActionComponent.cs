using Robust.Shared.Prototypes;

namespace Content.Server._BlackM.Speech;

// вернул вырез кнопку крика
[RegisterComponent, Access(typeof(ScreamActionSystem))]
public sealed partial class ScreamActionComponent : Component
{
    [DataField]
    public EntProtoId Action = "ActionScream";

    [DataField]
    public EntityUid? ActionEntity;
}
