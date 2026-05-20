using Content.Shared._BlackM.Passport;
using Content.Shared.Inventory;
using Content.Shared.Mind.Components;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Containers;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._BlackM.Passport;

public sealed class PassportSpawnSystem : EntitySystem
{
    [Dependency] private readonly PassportSystem       _passport = default!;
    [Dependency] private readonly InventorySystem      _inv      = default!;
    [Dependency] private readonly IPrototypeManager    _proto    = default!;
    [Dependency] private readonly ILocalizationManager _loc      = default!;
    [Dependency] private readonly IRobustRandom        _random   = default!;

    private const string PassportJobsPrototypeId = "PassportJobsDefault";

    private static readonly string[] CityKeys =
    {
        "passport-city-1",
        "passport-city-2",
        "passport-city-3",
        "passport-city-4",
    };

    private readonly List<EntityUid> _pending = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PassportComponent, EntGotInsertedIntoContainerMessage>(OnPassportInserted);
    }

    private void OnPassportInserted(EntityUid passportUid, PassportComponent comp, EntGotInsertedIntoContainerMessage args)
    {
        if (comp.IsBound)
            return;

        var wearer = args.Container.Owner;

        if (!HasComp<InventoryComponent>(wearer))
            return;

        if (!_pending.Contains(wearer))
            _pending.Add(wearer);
    }

    public override void Update(float frameTime)
    {
        if (_pending.Count == 0)
            return;

        if (!_proto.TryIndex<PassportJobsPrototype>(PassportJobsPrototypeId, out var jobsProto))
            return;

        var done = new List<EntityUid>();

        foreach (var mob in _pending)
        {
            if (!Exists(mob))
            {
                done.Add(mob);
                continue;
            }

            var jobId = GetJobId(mob);
            if (jobId == null)
                continue;

            done.Add(mob);

            if (!JobsContains(jobsProto.Jobs, jobId))
                continue;

            if (!_inv.TryGetSlotEntity(mob, "passport", out var slotEntity) || slotEntity == null)
                continue;

            var passportUid = slotEntity.Value;

            if (!TryComp<PassportComponent>(passportUid, out var comp) || comp.IsBound)
                continue;

            var fullName  = MetaData(mob).EntityName;
            var spaceIdx  = fullName.LastIndexOf(' ');
            var firstName = spaceIdx > 0 ? fullName[..spaceIdx]       : fullName;
            var surname   = spaceIdx > 0 ? fullName[(spaceIdx + 1)..] : string.Empty;

            var jobTitle = _proto.TryIndex<JobPrototype>(jobId, out var jobProto)
                ? _loc.GetString(jobProto.Name)
                : jobId;

            var cityKey = _random.Pick(CityKeys);

            _passport.FillPassport(passportUid, mob, firstName, surname, jobTitle, cityKey, comp, jobId);
        }

        foreach (var uid in done)
            _pending.Remove(uid);
    }

    private static bool JobsContains(HashSet<string> jobs, string jobId)
    {
        var lower = jobId.ToLowerInvariant();
        foreach (var j in jobs)
        {
            if (j.ToLowerInvariant() == lower)
                return true;
        }
        return false;
    }

    private string? GetJobId(EntityUid mob)
    {
        if (!TryComp<MindContainerComponent>(mob, out var mindContainer) || !mindContainer.HasMind)
            return null;

        if (!TryComp<MindComponent>(mindContainer.Mind!.Value, out var mind))
            return null;

        foreach (var roleEnt in mind.MindRoles)
        {
            if (TryComp<MindRoleComponent>(roleEnt, out var roleComp) && roleComp.JobPrototype != null)
                return roleComp.JobPrototype;
        }

        return null;
    }
}