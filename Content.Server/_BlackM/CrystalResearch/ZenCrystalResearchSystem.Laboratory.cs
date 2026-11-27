using Content.Server.Beam;
using Content.Shared._BlackM.CrystalResearch;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeviceLinking;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Content.Shared.Damage;
using Content.Shared.Popups;
using Content.Server.Power.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.Containers;

namespace Content.Server._BlackM.CrystalResearch;

public sealed partial class ZenCrystalResearchSystem
{
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private BatterySystem _battery = default!;
    [Dependency] private BeamSystem _beam = default!;

    private const string MediumSlot = "zen_growth_medium";
    private const string CoolantSlot = "zen_coolant";
    private const string CapacitorSlot = "zen_capacitor";
    private float _emitterUiAccumulator;

    private void InitializeLaboratory()
    {
        SubscribeLocalEvent<ZenCrystalLabToolComponent, AfterInteractEvent>(OnToolInteract);
        SubscribeLocalEvent<ZenCrystalLabToolComponent, UseInHandEvent>(OnToolUse);
        SubscribeLocalEvent<ZenCrystalLabToolComponent, ZenCrystalToolDoAfterEvent>(OnToolFinished);
        SubscribeLocalEvent<ZenCrystalChamberComponent, GetVerbsEvent<AlternativeVerb>>(OnChamberVerbs);
        SubscribeLocalEvent<ZenCrystalChamberComponent, MapInitEvent>(OnChamberInit);
        SubscribeLocalEvent<ZenCrystalChamberComponent, EntInsertedIntoContainerMessage>(OnMediumChanged);
        SubscribeLocalEvent<ZenCrystalEmitterComponent, ComponentShutdown>(OnEmitterShutdown);
        SubscribeLocalEvent<ZenGrowthMediumComponent, ExaminedEvent>(OnMediumExamined);
        SubscribeLocalEvent<ZenCrystalCoolantComponent, ExaminedEvent>(OnCoolantExamined);
        Subs.BuiEvents<ZenCrystalEmitterComponent>(ZenCrystalUiKey.Emitter, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnEmitterOpened);
            subs.Event<ZenCrystalEmitterCommandMessage>(OnEmitterCommand);
        });
    }

    private void OnChamberInit(Entity<ZenCrystalChamberComponent> ent, ref MapInitEvent args)
    {
        if (!ent.Comp.Resonator)
            _slots.SetLock(ent, MediumSlot, !ent.Comp.LidOpen);
    }

    private void OnMediumChanged(Entity<ZenCrystalChamberComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == MediumSlot)
            UpdateUi(ent);
    }

    private void OnMediumExamined(Entity<ZenGrowthMediumComponent> ent, ref ExaminedEvent args) =>
        args.PushMarkup(Loc.GetString(ent.Comp.Spent ? "zen-lab-medium-spent" : "zen-lab-medium-fresh"));

    private void OnCoolantExamined(Entity<ZenCrystalCoolantComponent> ent, ref ExaminedEvent args) =>
        args.PushMarkup(Loc.GetString("zen-lab-coolant-left", ("amount", (int) ent.Comp.Remaining)));

    private bool TryGetGrowthMedium(EntityUid machine, out Entity<ZenGrowthMediumComponent> medium)
    {
        medium = default;
        if (!_slots.TryGetSlot(machine, MediumSlot, out var slot) || slot.Item is not { } item ||
            !TryComp<ZenGrowthMediumComponent>(item, out var comp))
            return false;
        medium = (item, comp);
        return true;
    }

    private bool HasGrowthMedium(EntityUid machine, ZenCrystalResearchComponent sample) =>
        TryGetGrowthMedium(machine, out var medium) && medium.Comp.Medium == sample.Medium;

    private bool ConsumeGrowthMedium(Entity<ZenCrystalChamberComponent> ent, ZenGrowthMedium required)
    {
        if (!TryGetGrowthMedium(ent, out var medium) || medium.Comp.Medium != required || medium.Comp.Spent)
        {
            ent.Comp.Status = Loc.GetString("zen-lab-fresh-medium-required", ("medium", Loc.GetString(MediumKey(required))));
            return false;
        }
        medium.Comp.Spent = true;
        Dirty(medium);
        return true;
    }

    private void OnChamberVerbs(Entity<ZenCrystalChamberComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Resonator)
            return;
        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.LidOpen ? "zen-lab-close-lid" : "zen-lab-open-lid"),
            Act = () => ToggleLid(ent, user),
        });
    }

    private void ToggleLid(Entity<ZenCrystalChamberComponent> ent, EntityUid user)
    {
        if (!_interaction.InRangeUnobstructed(user, ent.Owner) || ent.Comp.Operation != null || ent.Comp.ActiveEmitter != null)
            return;
        ent.Comp.LidOpen = !ent.Comp.LidOpen;
        ent.Comp.Running = false;
        _slots.SetLock(ent, MediumSlot, !ent.Comp.LidOpen);
        if (!ent.Comp.LidOpen && TryCrystal(ent, out var sample) && sample.Comp.Stage == ZenCrystalStage.Growth &&
            sample.Comp.GrowthStarted)
        {
            if (NeedsService(sample.Comp) && sample.Comp.GrowthCheckpoints == 0 && ConsumeGrowthMedium(ent, sample.Comp.Medium))
            {
                sample.Comp.GrowthCheckpoints++;
                Log(sample.Comp, Loc.GetString("zen-lab-medium-replaced"), Name(user));
            }
            ent.Comp.Running = !NeedsService(sample.Comp) && HasGrowthMedium(ent, sample.Comp);
        }
        _popup.PopupEntity(Loc.GetString(ent.Comp.LidOpen ? "zen-lab-lid-open" : "zen-lab-lid-closed"), ent, user);
        UpdateUi(ent);
    }

    private bool ToolSample(EntityUid target, out Entity<ZenCrystalResearchComponent> sample)
    {
        sample = default;
        if (TryComp<ZenCrystalResearchComponent>(target, out var crystal))
        {
            sample = (target, crystal);
            return true;
        }
        return TryComp<ZenCrystalChamberComponent>(target, out var chamber) && TryCrystal((target, chamber), out sample);
    }

    private void OnToolInteract(Entity<ZenCrystalLabToolComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !ToolSample(target, out var sample))
            return;
        args.Handled = true;
        if (ent.Comp.Tool != ZenCrystalTool.Scanner)
        {
            if (!TryComp<ZenCrystalChamberComponent>(sample.Comp.ActiveMachine, out var chamber) || !chamber.LidOpen ||
                chamber.ActiveEmitter != null || chamber.Operation != null || chamber.Resonator)
            {
                _popup.PopupEntity(Loc.GetString("zen-lab-tool-needs-open-lid"), sample, args.User);
                return;
            }
            if (ent.Comp.Tool == ZenCrystalTool.Scraper &&
                (sample.Comp.Stage != ZenCrystalStage.Growth || sample.Comp.GrowthCheckpoints != 1 || !NeedsService(sample.Comp)))
                return;
            if (ent.Comp.Tool == ZenCrystalTool.Stabilizer && sample.Comp.Instability <= 0f)
                return;
        }
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay,
            new ZenCrystalToolDoAfterEvent { Step = sample.Comp.Step, Stage = sample.Comp.Stage },
            ent, target: sample.Owner, used: ent.Owner)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnToolFinished(Entity<ZenCrystalLabToolComponent> ent, ref ZenCrystalToolDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || !ToolSample(target, out var sample) ||
            sample.Comp.Step != args.Step || sample.Comp.Stage != args.Stage)
            return;
        args.Handled = true;
        if (ent.Comp.Tool == ZenCrystalTool.Scanner)
        {
            sample.Comp.ScannedStep = sample.Comp.Step;
            var report = sample.Comp.Stage == ZenCrystalStage.Structure && sample.Comp.Step < sample.Comp.ActivationDoses.Count
                ? ScanReport(sample.Comp) : sample.Comp.Stage == ZenCrystalStage.Growth ? GrowthClue(sample.Comp) :
                    Loc.GetString("zen-crystal-examine", ("stage", Loc.GetString(StageKey(sample.Comp.Stage))));
            Log(sample.Comp, report, Name(args.User));
            ent.Comp.Sample = sample.Owner;
            Spawn("ZenCrystalScanPulse", Transform(sample).Coordinates);
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/scan_finish.ogg"), sample);
            ShowScanner(ent, args.User);
        }
        else
        {
            if (!TryComp<ZenCrystalChamberComponent>(sample.Comp.ActiveMachine, out var chamber) || !chamber.LidOpen ||
                chamber.ActiveEmitter != null || chamber.Operation != null)
                return;
            if (ent.Comp.Tool == ZenCrystalTool.Scraper)
            {
                if (sample.Comp.GrowthCheckpoints != 1 || !NeedsService(sample.Comp))
                    return;
                sample.Comp.GrowthCheckpoints++;
                Log(sample.Comp, Loc.GetString("zen-lab-deposits-removed"), Name(args.User));
                Spawn("ZenCrystalDeposit", Transform(sample).Coordinates);
            }
            else
            {
                if (sample.Comp.Instability <= 0f || TerminatingOrDeleted(ent))
                    return;
                sample.Comp.Instability = Math.Max(0f, sample.Comp.Instability - 35f);
                Log(sample.Comp, Loc.GetString("zen-crystal-stabilized"), Name(args.User));
                Spawn("ZenCrystalColdPulse", Transform(sample).Coordinates);
                QueueDel(ent);
            }
            UpdateUi((sample.Comp.ActiveMachine!.Value, chamber));
        }
        UpdateVisuals(sample);
    }

    private string ScanReport(ZenCrystalResearchComponent sample)
    {
        var low = (int) (MathF.Floor(sample.ActivationDoses[sample.Step] / 10f) * 10f);
        var high = (int) (MathF.Ceiling((sample.ActivationDoses[sample.Step] + sample.DoseTolerances[sample.Step]) / 10f) * 10f);
        return Loc.GetString("zen-lab-scan-report",
            ("property", Loc.GetString("zen-lab-property-" + sample.Sequence[sample.Step].ToString().ToLowerInvariant())),
            ("low", low), ("high", high));
    }

    private void OnToolUse(Entity<ZenCrystalLabToolComponent> ent, ref UseInHandEvent args)
    {
        if (!args.Handled && ent.Comp.Tool == ZenCrystalTool.Scanner)
        {
            args.Handled = true;
            ShowScanner(ent, args.User);
        }
    }

    private void ShowScanner(Entity<ZenCrystalLabToolComponent> ent, EntityUid user)
    {
        if (!TryComp<ZenCrystalResearchComponent>(ent.Comp.Sample, out var crystal))
            return;
        var state = SampleState((ent.Comp.Sample!.Value, crystal));
        state.ReadOnly = true;
        state.Powered = true;
        _ui.SetUiState(ent.Owner, ZenCrystalUiKey.Chamber, state);
        _ui.TryOpenUi(ent.Owner, ZenCrystalUiKey.Chamber, user);
    }

    private bool FindChamber(Entity<ZenCrystalEmitterComponent> ent, out Entity<ZenCrystalChamberComponent> chamber)
    {
        chamber = default;
        if (!TryComp<DeviceLinkSourceComponent>(ent, out var source))
            return false;
        var found = false;
        foreach (var (uid, ports) in source.LinkedPorts)
        {
            var relevant = false;
            foreach (var pair in ports)
            {
                if (pair.Source == "ZenCrystalExposure" && pair.Sink == "ZenCrystalInput")
                    relevant = true;
            }
            if (!relevant || !TryComp<ZenCrystalChamberComponent>(uid, out var comp) || comp.Resonator)
                continue;
            if (found)
                return false;
            chamber = (uid, comp);
            found = true;
        }
        return found;
    }

    private void OnEmitterOpened(Entity<ZenCrystalEmitterComponent> ent, ref BoundUIOpenedEvent args) => UpdateEmitterUi(ent);
    private void OnEmitterShutdown(Entity<ZenCrystalEmitterComponent> ent, ref ComponentShutdown args) => StopEmitter(ent, false);

    private void OnEmitterCommand(Entity<ZenCrystalEmitterComponent> ent, ref ZenCrystalEmitterCommandMessage args)
    {
        if (!float.IsFinite(args.Power) || !float.IsFinite(args.Temperature) || args.Power is < 2f or > 12f ||
            args.Temperature is < -20f or > 80f || !this.IsPowered(ent, EntityManager) || !Transform(ent).Anchored)
            return;
        if (args.Toggle && ent.Comp.Active)
            StopEmitter(ent, true);
        else
        {
            if (!ent.Comp.Active)
            {
                ent.Comp.Power = args.Power;
                ent.Comp.TargetTemperature = args.Temperature;
            }
            if (args.Toggle)
                StartEmitter(ent, args.Actor);
        }
        Dirty(ent);
        UpdateEmitterUi(ent);
    }

    private void StartEmitter(Entity<ZenCrystalEmitterComponent> ent, EntityUid user)
    {
        if (!FindChamber(ent, out var chamber) || !_interaction.InRangeUnobstructed(ent.Owner, chamber.Owner, ent.Comp.Range))
        {
            ent.Comp.Status = Loc.GetString("zen-lab-link-required");
            return;
        }
        if (!TryCrystal(chamber, out var sample) || chamber.Comp.LidOpen || chamber.Comp.Operation != null ||
            !this.IsPowered(chamber, EntityManager) || !Transform(chamber).Anchored)
        {
            ent.Comp.Status = Loc.GetString("zen-lab-chamber-not-ready");
            return;
        }
        if (sample.Comp.Stage == ZenCrystalStage.Structure)
        {
            if (chamber.Comp.ActiveEmitter != null || sample.Comp.ScannedStep != sample.Comp.Step)
            {
                ent.Comp.Status = Loc.GetString("zen-lab-scan-required");
                return;
            }
            chamber.Comp.ActiveEmitter = ent.Owner;
            chamber.Comp.Exposure = ent.Comp.Exposure;
            chamber.Comp.AppliedDose = 0f;
            chamber.Comp.DoseOverloaded = false;
            if (sample.Comp.PulseAttempts++ > 0)
                ChangeInstability(chamber, sample, 4f);
        }
        else if (sample.Comp.Stage != ZenCrystalStage.Growth ||
                 ent.Comp.Exposure is not (ZenCrystalExposure.Heat or ZenCrystalExposure.Cold))
            return;
        ent.Comp.Chamber = chamber.Owner;
        ent.Comp.Operator = user;
        ent.Comp.Active = true;
        ent.Comp.Status = Loc.GetString("zen-lab-apparatus-running");
        _ambient.SetAmbience(ent, true);
        Dirty(ent);
    }

    private void StopEmitter(EntityUid uid, bool evaluate)
    {
        if (!TryComp<ZenCrystalEmitterComponent>(uid, out var emitter))
            return;
        emitter.Active = false;
        _ambient.SetAmbience(uid, false);
        if (TryComp<ZenCrystalChamberComponent>(emitter.Chamber, out var comp) && comp.ActiveEmitter == uid)
        {
            Entity<ZenCrystalChamberComponent> chamber = (emitter.Chamber!.Value, comp);
            comp.ActiveEmitter = null;
            if (TryCrystal(chamber, out var sample) && sample.Comp.Stage == ZenCrystalStage.Structure && comp.AppliedDose > 0f)
            {
                if (evaluate)
                    FinishPulse(chamber, sample, emitter);
                else
                    ChangeInstability(chamber, sample, 10f);
            }
            UpdateUi(chamber);
        }
        emitter.Chamber = null;
        Dirty(uid, emitter);
        UpdateEmitterUi((uid, emitter));
    }

    private void FinishPulse(Entity<ZenCrystalChamberComponent> chamber, Entity<ZenCrystalResearchComponent> sample,
        ZenCrystalEmitterComponent emitter)
    {
        var data = sample.Comp;
        var lower = data.ActivationDoses[data.Step];
        var upper = lower + data.DoseTolerances[data.Step];
        string result;
        if (emitter.Exposure != data.Sequence[data.Step])
        {
            ChangeInstability(chamber, sample, 25f);
            result = Loc.GetString("zen-lab-incompatible-exposure");
        }
        else if (chamber.Comp.DoseOverloaded || chamber.Comp.AppliedDose > upper)
            result = Loc.GetString("zen-lab-pulse-overloaded");
        else if (chamber.Comp.AppliedDose < lower)
        {
            ChangeInstability(chamber, sample, 8f);
            result = Loc.GetString("zen-lab-pulse-short");
        }
        else
        {
            data.Step++;
            data.PulseAttempts = 0;
            ChangeInstability(chamber, sample, 15f);
            result = Loc.GetString("zen-crystal-layer-opened", ("step", data.Step));
            Spawn("ZenCrystalResearchPulse", Transform(sample).Coordinates);
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/scan_finish.ogg"), sample);
            if (data.Step == data.Sequence.Count)
            {
                data.Stage = ZenCrystalStage.Growth;
                result += "\n" + GrowthClue(data);
            }
        }
        chamber.Comp.Status = result;
        Log(data, result, emitter.Operator is { } user && !Deleted(user) ? Name(user) : null);
        UpdateVisuals(sample);
    }

    private void ChangeInstability(Entity<ZenCrystalChamberComponent> chamber, Entity<ZenCrystalResearchComponent> sample, float amount)
    {
        var previous = sample.Comp.Instability;
        sample.Comp.Instability = Math.Max(0f, previous + amount);
        if (previous < 75f && sample.Comp.Instability >= 75f)
        {
            _popup.PopupEntity(Loc.GetString("zen-crystal-instability-warning"), sample, PopupType.LargeCaution);
            Spawn("ZenCrystalWarningPulse", Transform(sample).Coordinates);
            _audio.PlayPvs(new SoundCollectionSpecifier("sparks"), sample);
        }
        if (sample.Comp.Instability < 100f)
            return;
        sample.Comp.Instability = 60f;
        sample.Comp.Incidents++;
        sample.Comp.Quality = Math.Max(0.5f, sample.Comp.Quality - 0.1f);
        _damage.TryChangeDamage(chamber, new DamageSpecifier { DamageDict = new() { { "Heat", 10 } } });
        Spawn("EffectTeslaSparks", Transform(sample).Coordinates);
        Log(sample.Comp, Loc.GetString("zen-crystal-incident"));
        if (sample.Comp.Incidents >= 2)
        {
            Spawn("ZenCrystalBreachPulse", Transform(sample).Coordinates);
            Spawn("MobHeadcrabStationBlackM", Transform(chamber).Coordinates);
            _popup.PopupEntity(Loc.GetString("zen-lab-local-breach"), chamber, PopupType.LargeCaution);
        }
    }

    private string Reaction(Entity<ZenCrystalChamberComponent> chamber, ZenCrystalResearchComponent sample)
    {
        if (chamber.Comp.ActiveEmitter == null || sample.Stage != ZenCrystalStage.Structure || sample.Step >= sample.ActivationDoses.Count)
            return string.Empty;
        if (chamber.Comp.Exposure != sample.Sequence[sample.Step])
            return Loc.GetString("zen-lab-reaction-chaotic");
        if (chamber.Comp.AppliedDose > sample.ActivationDoses[sample.Step] + sample.DoseTolerances[sample.Step])
            return Loc.GetString("zen-lab-reaction-fracture");
        return Loc.GetString(chamber.Comp.AppliedDose >= sample.ActivationDoses[sample.Step]
            ? "zen-lab-reaction-stable" : "zen-lab-reaction-building");
    }

    private bool ConsumeApparatusResource(Entity<ZenCrystalEmitterComponent> ent, float frameTime)
    {
        if (ent.Comp.Exposure == ZenCrystalExposure.Electricity)
            return _slots.TryGetSlot(ent, CapacitorSlot, out var slot) && slot.Item is { } cell &&
                   _battery.TryUseCharge(cell, ent.Comp.Power * 4f * frameTime);
        if (ent.Comp.Exposure != ZenCrystalExposure.Cold)
            return true;
        if (!_slots.TryGetSlot(ent, CoolantSlot, out var coolantSlot) || coolantSlot.Item is not { } item ||
            !TryComp<ZenCrystalCoolantComponent>(item, out var coolant))
            return false;
        var spent = (0.2f + ent.Comp.Power * 0.03f) * frameTime;
        if (coolant.Remaining < spent)
            return false;
        coolant.Remaining -= spent;
        Dirty(item, coolant);
        return true;
    }

    private void UpdateLaboratory(float frameTime)
    {
        _emitterUiAccumulator += frameTime;
        var updateUi = _emitterUiAccumulator >= 0.25f;
        if (updateUi)
            _emitterUiAccumulator = 0f;
        var chambers = EntityQueryEnumerator<ZenCrystalChamberComponent>();
        while (chambers.MoveNext(out _, out var chamber))
            chamber.Temperature += (20f - chamber.Temperature) * Math.Min(1f, frameTime * 0.006f);

        var query = EntityQueryEnumerator<ZenCrystalEmitterComponent>();
        while (query.MoveNext(out var uid, out var emitter))
        {
            Entity<ZenCrystalEmitterComponent> ent = (uid, emitter);
            if (emitter.Active)
            {
                if (!FindChamber(ent, out var chamber) || chamber.Owner != emitter.Chamber || chamber.Comp.LidOpen ||
                    !Transform(uid).Anchored || !Transform(chamber).Anchored ||
                    !this.IsPowered(uid, EntityManager) || !this.IsPowered(chamber, EntityManager) ||
                    !_interaction.InRangeUnobstructed(uid, chamber.Owner, emitter.Range) ||
                    !TryCrystal(chamber, out var sample) || !ConsumeApparatusResource(ent, frameTime))
                {
                    emitter.Status = Loc.GetString("zen-lab-apparatus-interrupted");
                    StopEmitter(uid, false);
                    continue;
                }
                if (sample.Comp.Stage == ZenCrystalStage.Structure && chamber.Comp.ActiveEmitter == uid)
                {
                    chamber.Comp.AppliedDose += emitter.Power * frameTime;
                    if (emitter.Exposure != sample.Comp.Sequence[sample.Comp.Step])
                        ChangeInstability(chamber, sample, 4f * frameTime);
                    else if (chamber.Comp.AppliedDose > sample.Comp.ActivationDoses[sample.Comp.Step] + sample.Comp.DoseTolerances[sample.Comp.Step])
                    {
                        ChangeInstability(chamber, sample, chamber.Comp.DoseOverloaded ? 6f * frameTime : 25f);
                        chamber.Comp.DoseOverloaded = true;
                    }
                }
                else if (sample.Comp.Stage == ZenCrystalStage.Growth)
                {
                    var direction = emitter.Exposure == ZenCrystalExposure.Heat ? 1f : -1f;
                    var delta = emitter.TargetTemperature - chamber.Comp.Temperature;
                    if (delta * direction > 0f)
                        chamber.Comp.Temperature += direction * Math.Min(Math.Abs(delta), emitter.Power * frameTime * 0.6f);
                    if (emitter.Exposure == ZenCrystalExposure.Cold)
                        ChangeInstability(chamber, sample, -frameTime * 0.2f);
                }
                else
                {
                    StopEmitter(uid, false);
                    continue;
                }
                emitter.EffectAccumulator += frameTime;
                if (emitter.EffectAccumulator >= 0.65f)
                {
                    emitter.EffectAccumulator = 0f;
                    var suffix = emitter.Exposure.ToString();
                    _beam.TryCreateBeam(uid, sample.Owner, "ZenCrystalBeam" + suffix);
                    Spawn("ZenCrystal" + suffix + "Pulse", Transform(sample).Coordinates);
                }
            }
            if (updateUi)
                UpdateEmitterUi(ent);
        }
    }

    private void UpdateEmitterUi(Entity<ZenCrystalEmitterComponent> ent)
    {
        var state = new ZenCrystalEmitterState
        {
            Exposure = ent.Comp.Exposure,
            Power = ent.Comp.Power,
            TargetTemperature = ent.Comp.TargetTemperature,
            Active = ent.Comp.Active,
            Powered = this.IsPowered(ent, EntityManager) && Transform(ent).Anchored,
            Status = ent.Comp.Status,
            Consumable = -1f,
        };
        if (FindChamber(ent, out var chamber))
        {
            state.Connected = true;
            state.Target = Name(chamber);
            state.Dose = chamber.Comp.AppliedDose;
            if (TryCrystal(chamber, out var sample))
            {
                state.Instability = sample.Comp.Instability;
                if (ent.Comp.Active)
                    state.Status = sample.Comp.Stage == ZenCrystalStage.Structure ? Reaction(chamber, sample.Comp) :
                        Loc.GetString("zen-lab-temperature-live", ("temperature", MathF.Round(chamber.Comp.Temperature, 1)));
            }
        }
        if (ent.Comp.Exposure == ZenCrystalExposure.Cold && _slots.TryGetSlot(ent, CoolantSlot, out var coolantSlot) &&
            TryComp<ZenCrystalCoolantComponent>(coolantSlot.Item, out var coolant))
            state.Consumable = coolant.Remaining;
        if (ent.Comp.Exposure == ZenCrystalExposure.Electricity && _slots.TryGetSlot(ent, CapacitorSlot, out var slot) &&
            TryComp<Content.Shared.Power.Components.BatteryComponent>(slot.Item, out var battery))
            state.Consumable = battery.CurrentCharge;
        _ui.SetUiState(ent.Owner, ZenCrystalUiKey.Emitter, state);
    }
}
