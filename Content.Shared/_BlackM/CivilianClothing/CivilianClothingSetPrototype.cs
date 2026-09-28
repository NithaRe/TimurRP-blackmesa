using Content.Shared.Humanoid; // BlackM: Sex
using Robust.Shared.Prototypes;

namespace Content.Shared._BlackM.CivilianClothing;

[Prototype]
public sealed partial class CivilianClothingSetPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField(required: true)]
    public LocId Name { get; private set; }

    [DataField]
    public int Priority { get; private set; }

    [DataField]
    public Dictionary<string, List<EntProtoId>> Equipment { get; set; } = new();

    [DataField]
    public Dictionary<string, List<EntProtoId>> FemaleEquipment { get; set; } = new();

    public EntProtoId? GetGearSeeded(string slot, int seed, Sex sex)
    {
        List<EntProtoId>? options = null;

        if (sex == Sex.Female && FemaleEquipment.TryGetValue(slot, out var femaleOptions) && femaleOptions.Count > 0)
        {
            options = femaleOptions;
        }
        else if (Equipment.TryGetValue(slot, out var defaultOptions) && defaultOptions.Count > 0)
        {
            options = defaultOptions;
        }

        if (options == null)
            return null;

        var rng = new System.Random(HashCode.Combine(seed, slot));
        return options[rng.Next(options.Count)];
    }
}