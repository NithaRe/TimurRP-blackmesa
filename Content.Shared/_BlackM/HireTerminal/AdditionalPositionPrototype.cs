using Robust.Shared.Prototypes;

namespace Content.Shared._BlackM.HireTerminal;

[Prototype("additionalPosition")]
public sealed class AdditionalPositionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = default!;

    [DataField(required: true)]
    public string Name { get; private set; } = default!;

    [DataField(required: true)]
    public string Department { get; private set; } = default!;

    [DataField]
    public int DepartmentOrder { get; private set; }

    [DataField]
    public string? Description { get; private set; }

    [DataField(required: true)]
    public EntProtoId Badge { get; private set; }

    [DataField]
    public Color Color { get; private set; } = Color.White;
}
