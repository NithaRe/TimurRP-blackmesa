using Robust.Shared.Prototypes;

namespace Content.Shared._BlackM.Passport;

[Prototype("passportJobs")]
public sealed class PassportJobsPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = default!;

    [DataField("jobs")]
    public HashSet<string> Jobs { get; } = new();
}