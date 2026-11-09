using Content.Shared._BlackM.FoodVendor;
using Content.Shared._BlackM.Passport;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Roles.Jobs;
using Content.Shared.VendingMachines;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.FoodVendor;

/// <summary>
/// Periodically looks for a living, connected cook and locks or unlocks food vendors.
/// </summary>
public sealed class FoodVendorCookSystem : EntitySystem
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(2);

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;

    private readonly HashSet<string> _jobsPresent = new();
    private readonly HashSet<string> _positionsPresent = new();
    private TimeSpan _nextCheck;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextCheck)
            return;

        _nextCheck = _timing.CurTime + CheckInterval;

        var scanned = false;
        var query = EntityQueryEnumerator<FoodVendorComponent, VendingMachineComponent>();
        while (query.MoveNext(out var uid, out var comp, out var vend))
        {
            if (!scanned)
            {
                ScanCooks();
                scanned = true;
            }

            var cook = HasCook(comp);
            var locked = comp.LockWhenCookPresent ? cook : !cook;

            if (comp.Locked != locked)
            {
                comp.Locked = locked;
                Dirty(uid, comp);
            }

            _appearance.SetData(uid, FoodVendorVisuals.Locked, locked && !vend.Broken && _power.IsPowered(uid));
        }
    }

    private void ScanCooks()
    {
        _jobsPresent.Clear();
        _positionsPresent.Clear();

        var mobs = EntityQueryEnumerator<MindContainerComponent, ActorComponent, MobStateComponent>();
        while (mobs.MoveNext(out var uid, out var mindContainer, out _, out var mobState))
        {
            if (mindContainer.Mind is not { } mind || _mobState.IsDead(uid, mobState))
                continue;

            if (_jobs.MindTryGetJobId(mind, out var job) && job is { } jobId)
                _jobsPresent.Add(jobId.Id);
        }

        // Cooks hired through the hire terminal keep their original job, check passports.
        var passports = EntityQueryEnumerator<PassportComponent>();
        while (passports.MoveNext(out _, out var passport))
        {
            if (string.IsNullOrEmpty(passport.AdditionalJobId) || passport.OwnerEntity is not { } ownerNet)
                continue;

            if (!TryGetEntity(ownerNet, out var owner) || !IsLivePlayer(owner.Value))
                continue;

            _positionsPresent.Add(passport.AdditionalJobId);
        }
    }

    private bool IsLivePlayer(EntityUid uid)
    {
        return HasComp<ActorComponent>(uid)
               && TryComp<MobStateComponent>(uid, out var mobState)
               && !_mobState.IsDead(uid, mobState);
    }

    private bool HasCook(FoodVendorComponent comp)
    {
        foreach (var job in comp.CookJobs)
        {
            if (_jobsPresent.Contains(job.Id))
                return true;
        }

        foreach (var position in comp.CookPositions)
        {
            if (_positionsPresent.Contains(position))
                return true;
        }

        return false;
    }
}
