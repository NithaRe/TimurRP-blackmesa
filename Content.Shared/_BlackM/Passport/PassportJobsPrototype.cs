using Robust.Shared.Prototypes;

namespace Content.Shared._BlackM.Passport;

[Prototype("passportJobs")]
public sealed partial class PassportJobsPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("jobs")]
    public HashSet<string> Jobs { get; private set; } = new();
}