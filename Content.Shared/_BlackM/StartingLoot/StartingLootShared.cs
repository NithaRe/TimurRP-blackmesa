using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._BlackM.StartingLoot;

[DataDefinition]
public sealed partial class StartingLootEntry
{
    [DataField(required: true)]
    public EntProtoId Id = default!;

    [DataField]
    public float Weight = 1f;
}

[Prototype]
public sealed partial class StartingLootPoolPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<StartingLootEntry> Entries = new();

    [DataField]
    public int Rolls = 2;
}

[Serializable, NetSerializable]
public sealed class StartingLootRolledEvent : EntityEventArgs
{
    public List<EntProtoId> Results;

    public StartingLootRolledEvent(List<EntProtoId> results)
    {
        Results = results;
    }
}
