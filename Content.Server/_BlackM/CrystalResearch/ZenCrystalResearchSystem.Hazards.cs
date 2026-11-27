using Content.Shared._BlackM.CrystalResearch;
using Content.Shared.Damage;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio;

namespace Content.Server._BlackM.CrystalResearch;

public sealed partial class ZenCrystalResearchSystem
{
    private const float RunawayThreshold = 75f;
    private const float BreachDelay = 10f;

    private static bool HasGrowthBonus(ZenCrystalResearchComponent sample) =>
        sample.AcceleratedGrowthSeconds >= sample.GrowthDuration * 0.5f;

    public static string HazardStatus(ZenCrystalResearchComponent sample)
    {
        if (!sample.Runaway)
            return string.Empty;
        return sample.BreachRemaining > 0f
            ? Robust.Shared.Localization.Loc.GetString("zen-lab-breach-countdown", ("seconds", (int) Math.Ceiling(sample.BreachRemaining)))
            : Robust.Shared.Localization.Loc.GetString("zen-lab-runaway-status");
    }

    private void BeginRunaway(Entity<ZenCrystalResearchComponent> sample)
    {
        if (sample.Comp.Runaway || sample.Comp.Stage is ZenCrystalStage.Researched or ZenCrystalStage.Exhausted)
            return;
        sample.Comp.Runaway = true;
        sample.Comp.AcceleratedGrowth = false;
        sample.Comp.HazardAccumulator = 0f;
        sample.Comp.BreachRemaining = sample.Comp.Instability >= 100f ? BreachDelay : 0f;
        if (TryComp<ZenCrystalChamberComponent>(sample.Comp.ActiveMachine, out var chamber))
        {
            chamber.Running = false;
            chamber.Operation = null;
            if (chamber.ActiveEmitter is { } uid && TryComp<ZenCrystalEmitterComponent>(uid, out var emitter))
            {
                if (emitter.Exposure == ZenCrystalExposure.Cold)
                    emitter.ContainmentCooling = true;
                else
                    StopEmitter(uid, false);
            }
        }
        Log(sample.Comp, Loc.GetString("zen-lab-runaway-started"));
        _popup.PopupEntity(HazardStatus(sample.Comp), sample, PopupType.LargeCaution);
        Spawn("ZenCrystalWarningPulse", Transform(sample).Coordinates);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/alarm.ogg"), sample);
        UpdateVisuals(sample);
    }

    // The reaction belongs to the sample: power loss, extraction and moving it cannot reset the countdown.
    private void UpdateHazards(float frameTime)
    {
        var query = EntityQueryEnumerator<ZenCrystalResearchComponent>();
        while (query.MoveNext(out var uid, out var crystal))
        {
            if (crystal.Stage is ZenCrystalStage.Researched or ZenCrystalStage.Exhausted)
                continue;
            Entity<ZenCrystalResearchComponent> sample = (uid, crystal);
            if (!crystal.Runaway && crystal.Instability >= RunawayThreshold)
                BeginRunaway(sample);
            if (!crystal.Runaway)
                continue;

            if (crystal.Instability <= 60f && crystal.Temperature <= 35f)
            {
                crystal.Runaway = false;
                crystal.BreachRemaining = 0f;
                Log(crystal, Loc.GetString("zen-lab-contained"));
                _popup.PopupEntity(Loc.GetString("zen-lab-contained"), sample, PopupType.Medium);
                if (TryComp<ZenCrystalChamberComponent>(crystal.ActiveMachine, out var recovered))
                    recovered.Status = Loc.GetString("zen-lab-contained");
                UpdateVisuals(sample);
                continue;
            }

            crystal.Temperature = Math.Min(120f, crystal.Temperature + frameTime * 1.5f);
            crystal.Instability = Math.Min(100f, crystal.Instability + frameTime);
            if (crystal.BreachRemaining > 0f)
            {
                if (crystal.Instability < 85f)
                {
                    crystal.BreachRemaining = 0f;
                    Log(crystal, Loc.GetString("zen-lab-countdown-cancelled"));
                }
                else
                {
                    crystal.BreachRemaining = Math.Max(0f, crystal.BreachRemaining - frameTime);
                    if (crystal.BreachRemaining <= 0f)
                    {
                        Breach(sample);
                        continue;
                    }
                }
            }
            else if (crystal.Instability >= 100f)
            {
                crystal.BreachRemaining = BreachDelay;
                Log(crystal, HazardStatus(crystal));
                _popup.PopupEntity(HazardStatus(crystal), sample, PopupType.LargeCaution);
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/alarm.ogg"), sample);
            }

            crystal.HazardAccumulator += frameTime;
            if (crystal.HazardAccumulator >= 2f)
            {
                crystal.HazardAccumulator %= 2f;
                Spawn("ZenCrystalWarningPulse", Transform(sample).Coordinates);
                _audio.PlayPvs(new SoundCollectionSpecifier("sparks"), sample);
                var enclosed = TryComp<ZenCrystalChamberComponent>(crystal.ActiveMachine, out var chamber) && !chamber.LidOpen;
                if (!enclosed)
                    DamageNearby(sample, 2f, 6, 0);
                UpdateVisuals(sample);
            }
        }
    }

    private void DamageNearby(EntityUid source, float radius, int heat, int shock)
    {
        var damage = new DamageSpecifier { DamageDict = new() { { "Heat", heat }, { "Shock", shock } } };
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(_transform.GetMapCoordinates(source), radius))
        {
            if (_interaction.InRangeUnobstructed(source, mob.Owner, radius))
                _damage.TryChangeDamage(mob.Owner, damage, origin: source, canMiss: false);
        }
    }

    private void Breach(Entity<ZenCrystalResearchComponent> sample)
    {
        var coordinates = _transform.GetMapCoordinates(sample.Owner);
        var chamberUid = sample.Comp.ActiveMachine;
        sample.Comp.Runaway = false;
        sample.Comp.BreachRemaining = 0f;
        sample.Comp.Incidents++;
        sample.Comp.Stage = ZenCrystalStage.Exhausted;
        sample.Comp.Energy = 0f;
        sample.Comp.Quality = 0f;
        sample.Comp.AcceleratedGrowth = false;
        sample.Comp.CurrentTask = Loc.GetString("zen-lab-task-recycle");
        EnsureComp<ZenCrystalSpentComponent>(sample);
        if (TryComp<ZenCrystalChamberComponent>(chamberUid, out var chamber))
        {
            chamber.Running = false;
            chamber.Operation = null;
            chamber.Status = Loc.GetString("zen-lab-breach-finished");
            if (chamber.ActiveEmitter is { } emitter)
                StopEmitter(emitter, false);
        }
        Log(sample.Comp, Loc.GetString("zen-lab-breach-finished"));
        UpdateVisuals(sample);
        Spawn("ZenCrystalBreachPulse", coordinates);
        Spawn("EffectTeslaSparks", coordinates);
        _popup.PopupEntity(Loc.GetString("zen-lab-breach-finished"), sample, PopupType.LargeCaution);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/alarm.ogg"), sample);
        DamageNearby(sample, 3f, 20, 10);

        var equipmentDamage = new DamageSpecifier { DamageDict = new() { { "Heat", 80 } } };
        foreach (var machine in _lookup.GetEntitiesInRange<ZenCrystalChamberComponent>(coordinates, 3f))
            _damage.TryChangeDamage(machine.Owner, equipmentDamage);
        foreach (var emitter in _lookup.GetEntitiesInRange<ZenCrystalEmitterComponent>(coordinates, 3f))
            _damage.TryChangeDamage(emitter.Owner, equipmentDamage);
        Spawn("MobHeadcrabStationBlackM", coordinates);
        Spawn("MobHeadcrabStationBlackM", coordinates);
        Spawn("MobHoundeyeStationBlackM", coordinates);
    }

    private void Quench(Entity<ZenCrystalChamberComponent> chamber, Entity<ZenCrystalResearchComponent> sample, EntityUid user)
    {
        if (chamber.Comp.Resonator || (!sample.Comp.Runaway && sample.Comp.Instability < 65f) ||
            sample.Comp.Stage is ZenCrystalStage.Researched or ZenCrystalStage.Exhausted)
            return;
        sample.Comp.Instability = 0f;
        sample.Comp.Temperature = 20f;
        ExhaustSample(sample, chamber, "zen-lab-quenched");
        Log(sample.Comp, Loc.GetString("zen-lab-quench-author"), Name(user));
        Spawn("ZenCrystalColdPulse", Transform(sample).Coordinates);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/scan_finish.ogg"), chamber);
        _popup.PopupEntity(Loc.GetString("zen-lab-quenched"), chamber, PopupType.Medium);
        UpdateUi(chamber);
    }
}
