using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._BlackM.Ams;

[RegisterComponent]
public sealed partial class AmsPartComponent : Component
{
    [DataField(required: true)]
    public AmsPartType PartType;
}
