using Content.Shared._BlackM.CrystalResearch;
using Robust.Shared.Prototypes;

namespace Content.Server._BlackM.CrystalResearch;

[RegisterComponent]
public sealed partial class ZenResearchStationComponent : Component
{
    [DataField] public int Breakthroughs;
    [DataField] public Dictionary<string, int> Perks = new();
    [DataField] public Dictionary<string, int> Purchased = new();
    [DataField] public List<ZenResearchDelivery> Deliveries = new();
    [DataField] public int NextOrderNumber = 1;
    [DataField] public List<EntityUid> Samples = new();
    [DataField] public int NextSampleNumber = 1;
}

[DataDefinition]
public sealed partial class ZenResearchDelivery
{
    [DataField] public int Number;
    [DataField] public ProtoId<ZenResearchSupplyPrototype> Supply;
    [DataField] public EntityUid Destination;
    [DataField] public float Remaining;
    [DataField] public bool Delivered;
}

// Membership belongs to the mind, not the current body or its physical location.
[RegisterComponent]
public sealed partial class ZenResearchCrewMemberComponent : Component
{
    [DataField] public EntityUid Station;
}
