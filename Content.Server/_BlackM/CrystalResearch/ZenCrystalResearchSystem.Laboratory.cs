using Content.Server.Beam;
using Content.Shared._BlackM.CrystalResearch;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeviceLinking;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
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
    [Dependency] private EntityLookupSystem _lookup = default!;

    private const string MediumSlot = "zen_growth_medium";
    private const string SampleSlot = "zen_crystal_sample";
    private const string CoolantSlot = "zen_coolant";
    private const string CapacitorSlot = "zen_capacitor";
    private float _emitterUiAccumulator;

    private void InitializeLaboratory()
    {
        SubscribeLocalEvent<ZenCrystalLabToolComponent, AfterInteractEvent>(OnToolInteract);
        SubscribeLocalEvent<ZenCrystalLabToolComponent, ZenCrystalToolDoAfterEvent>(OnToolFinished);
        SubscribeLocalEvent<ZenCrystalChamberComponent, GetVerbsEvent<AlternativeVerb>>(OnChamberVerbs);
        SubscribeLocalEvent<ZenCrystalChamberComponent, MapInitEvent>(OnChamberInit);
        SubscribeLocalEvent<ZenCrystalChamberComponent, ZenCrystalCloseLidEvent>(OnCloseLid);
        SubscribeLocalEvent<ZenCrystalEmitterComponent, ComponentShutdown>(OnEmitterShutdown);
        SubscribeLocalEvent<ZenGrowthMediumComponent, ExaminedEvent>(OnMediumExamined);
        SubscribeLocalEvent<ZenCrystalCoolantComponent, ExaminedEvent>(OnCoolantExamined);
        Subs.BuiEvents<ZenCrystalEmitterComponent>(ZenCrystalUiKey.Emitter,
            subs =>
            {
                subs.Event<BoundUIOpenedEvent>(OnEmitterOpened);
                subs.Event<ZenCrystalEmitterCommandMessage>(OnEmitterCommand);
            });
    }

    private void OnChamberInit(Entity<ZenCrystalChamberComponent> ent, ref MapInitEvent args)
    {
        if (!ent.Comp.Resonator)
        {
            _slots.SetLock(ent, MediumSlot, !ent.Comp.LidOpen);
            _slots.SetLock(ent, SampleSlot, !ent.Comp.LidOpen);
        }
        UpdateSampleVisibility(ent);
    }

    private void UpdateSampleVisibility(Entity<ZenCrystalChamberComponent> ent)
    {
        if (!_slots.TryGetSlot(ent, SampleSlot, out var slot) || slot.ContainerSlot is not { } container)
            return;
        var visible = !ent.Comp.Resonator && ent.Comp.LidOpen;
        if (container.ShowContents == visible)
            return;
        container.ShowContents = visible;
        Dirty(ent.Owner, Comp<ContainerManagerComponent>(ent));
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

    private void FeedCurrentTask(Entity<ZenCrystalChamberComponent> ent, Entity<ZenCrystalResearchComponent> sample, EntityUid user)
    {
        if (sample.Comp.Runaway || sample.Comp.Stage != ZenCrystalStage.Structure || sample.Comp.ScannedStep != sample.Comp.Step ||
            sample.Comp.Step >= sample.Comp.Tasks.Count || sample.Comp.Tasks[sample.Comp.Step].Type != ZenCrystalTaskType.Medium)
            return;
        var task = sample.Comp.Tasks[sample.Comp.Step];
        if (!TryGetGrowthMedium(ent, out var medium) || medium.Comp.Spent)
        {
            ent.Comp.Status = Loc.GetString("zen-lab-fresh-medium-required", ("medium", Loc.GetString(MediumKey(task.Medium))));
            return;
        }
        medium.Comp.Spent = true;
        Dirty(medium);
        if (medium.Comp.Medium != task.Medium)
        {
            FailedTask(ent, sample, "zen-lab-wrong-medium", 20f);
            return;
        }
        CompleteTask(ent, sample, Name(user));
    }

    private void OnCloseLid(Entity<ZenCrystalChamberComponent> ent, ref ZenCrystalCloseLidEvent args)
    {
        if (ent.Comp.Resonator || !ent.Comp.LidOpen)
            return;
        ToggleLid(ent, args.User);
    }

    private void OnChamberVerbs(Entity<ZenCrystalChamberComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Resonator)
            return;
        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.LidOpen ? "zen-lab-close-lid" : "zen-lab-open-lid"),
            Priority = 20,
            Act = () => ToggleLid(ent, user),
        });
        if (TryCrystal(ent, out var sample) && sample.Comp.Runaway)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("zen-lab-quench"),
                Act = () =>
                {
                    if (_interaction.InRangeUnobstructed(user, ent.Owner) && TryCrystal(ent, out var current))
                        Quench(ent, current, user);
                },
            });
        }
    }

    private void ToggleLid(Entity<ZenCrystalChamberComponent> ent, EntityUid user)
    {
        if (!_interaction.InRangeUnobstructed(user, ent.Owner) || ent.Comp.Operation != null ||
            ent.Comp.ActiveEmitter != null)
            return;
        ent.Comp.LidOpen = !ent.Comp.LidOpen;
        Dirty(ent);
        UpdateSampleVisibility(ent);
        ent.Comp.Running = false;
        _slots.SetLock(ent, MediumSlot, !ent.Comp.LidOpen);
        _slots.SetLock(ent, SampleSlot, !ent.Comp.LidOpen);
        if (ent.Comp.LidOpen && TryCrystal(ent, out var exposed) && exposed.Comp.Runaway)
        {
            DamageNearby(ent, 2f, 8, 0);
            Spawn("ZenCrystalHeatPulse", Transform(ent).Coordinates);
            _popup.PopupEntity(Loc.GetString("zen-lab-lid-vent"), ent, PopupType.LargeCaution);
        }
        if (!ent.Comp.LidOpen && TryCrystal(ent, out var sample))
        {
            if (sample.Comp.Stage == ZenCrystalStage.Growth && !sample.Comp.Runaway)
                ent.Comp.Running = true;
            else
                FeedCurrentTask(ent, sample, user);
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

        return TryComp<ZenCrystalChamberComponent>(target, out var chamber) &&
               TryCrystal((target, chamber), out sample);
    }

    private void OnToolInteract(Entity<ZenCrystalLabToolComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;
        if (ent.Comp.Tool == ZenCrystalTool.Splitter)
        {
            if (!TryComp<ZenCrystalRawComponent>(target, out var raw) || raw.Split)
                return;
            args.Handled = true;
            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay,
                new ZenCrystalToolDoAfterEvent(), ent, target: target, used: ent.Owner)
            {
                BreakOnDamage = true,
                BreakOnMove = true,
                NeedHand = true,
            });
            return;
        }
        if (!ToolSample(target, out var sample))
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

            if (sample.Comp.Stage is ZenCrystalStage.Researched or ZenCrystalStage.Exhausted)
                return;
            if (ent.Comp.Tool == ZenCrystalTool.Stabilizer && sample.Comp.Instability <= 0f)
                return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager,
            args.User,
            ent.Comp.Delay,
            new ZenCrystalToolDoAfterEvent
            {
                Step = sample.Comp.Step, Stage = sample.Comp.Stage, Sample = GetNetEntity(sample.Owner),
            },
            ent,
            target: target,
            used: ent.Owner)
        {
            BreakOnDamage = ent.Comp.Tool != ZenCrystalTool.Stabilizer || !sample.Comp.Runaway,
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnToolFinished(Entity<ZenCrystalLabToolComponent> ent, ref ZenCrystalToolDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || TerminatingOrDeleted(target))
            return;
        if (ent.Comp.Tool == ZenCrystalTool.Splitter)
        {
            if (!TryComp<ZenCrystalRawComponent>(target, out var raw) || raw.Split ||
                !_interaction.InRangeUnobstructed(args.User, target))
                return;
            args.Handled = true;
            raw.Split = true;
            var coordinates = Transform(target).Coordinates;
            Spawn("ZenCrystalSample", coordinates.Offset(new System.Numerics.Vector2(-0.15f, 0f)));
            Spawn("ZenCrystalSample", coordinates.Offset(new System.Numerics.Vector2(0.15f, 0f)));
            Spawn("ZenCrystalScanPulse", coordinates);
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/scan_finish.ogg"), target);
            QueueDel(target);
            return;
        }
        if (!ToolSample(target, out var sample) || GetNetEntity(sample.Owner) != args.Sample ||
            sample.Comp.Step != args.Step || sample.Comp.Stage != args.Stage)
            return;
        args.Handled = true;
        if (ent.Comp.Tool == ZenCrystalTool.Scanner)
        {
            if (!_zenResearch.RegisterSample(sample, args.User))
                return;
            sample.Comp.ScannedStep = sample.Comp.Step;
            sample.Comp.CurrentTask = sample.Comp.Stage == ZenCrystalStage.Structure
                ? ScanReport(sample.Comp) : Loc.GetString(sample.Comp.Stage == ZenCrystalStage.Growth
                    ? "zen-lab-task-growing" : sample.Comp.Stage == ZenCrystalStage.Mature
                        ? "zen-lab-task-finalize" : sample.Comp.Stage == ZenCrystalStage.Exhausted
                            ? "zen-lab-task-recycle" : "zen-lab-task-resonate");
            Log(sample.Comp, sample.Comp.CurrentTask, Name(args.User));

            if (TryComp<ZenCrystalChamberComponent>(sample.Comp.ActiveMachine, out var camera))
            {
                camera.Status = Loc.GetString("zen-lab-protocol-ready");
                UpdateUi((sample.Comp.ActiveMachine!.Value, camera));
            }

            Spawn("ZenCrystalScanPulse", Transform(sample).Coordinates);
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/scan_finish.ogg"), sample);
            _popup.PopupEntity(Loc.GetString("zen-lab-scan-uploaded", ("number", sample.Comp.SampleNumber)), ent, args.User);
        }
        else
        {
            if (!TryComp<ZenCrystalChamberComponent>(sample.Comp.ActiveMachine, out var chamber) || !chamber.LidOpen ||
                chamber.ActiveEmitter != null || chamber.Operation != null)
                return;
            if (ent.Comp.Tool == ZenCrystalTool.Scraper)
            {
                Entity<ZenCrystalChamberComponent> machine = (sample.Comp.ActiveMachine!.Value, chamber);
                if (sample.Comp.Stage != ZenCrystalStage.Structure || sample.Comp.ScannedStep != sample.Comp.Step ||
                    sample.Comp.Step >= sample.Comp.Tasks.Count || sample.Comp.Tasks[sample.Comp.Step].Type != ZenCrystalTaskType.Scrape)
                    FailedTask(machine, sample, "zen-lab-wrong-scrape", 20f);
                else
                {
                    Spawn("ZenCrystalDeposit", Transform(machine).Coordinates);
                    CompleteTask(machine, sample, Name(args.User));
                }
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
        if (sample.Step >= sample.Tasks.Count)
            return Loc.GetString("zen-lab-task-finalize");
        var task = sample.Tasks[sample.Step];
        return task.Type switch
        {
            ZenCrystalTaskType.Temperature => Loc.GetString("zen-lab-task-temperature",
                ("target", task.Target.ToString("F1")), ("tolerance", task.Tolerance.ToString("F1"))),
            ZenCrystalTaskType.Electricity or ZenCrystalTaskType.Radiation => Loc.GetString("zen-lab-task-dose",
                ("apparatus", Loc.GetString(task.Type == ZenCrystalTaskType.Electricity ? "ent-ZenCrystalDischarger" : "ent-ZenCrystalIrradiator")),
                ("low", task.Target.ToString("F1")), ("high", (task.Target + task.Tolerance).ToString("F1"))),
            ZenCrystalTaskType.Medium => Loc.GetString("zen-lab-task-medium", ("medium", Loc.GetString(MediumKey(task.Medium)))),
            _ => Loc.GetString("zen-lab-task-scrape"),
        };
    }

    private static bool MatchesTask(ZenCrystalExposure exposure, ZenCrystalResearchTask task) => task.Type switch
    {
        ZenCrystalTaskType.Temperature => exposure is ZenCrystalExposure.Heat or ZenCrystalExposure.Cold,
        ZenCrystalTaskType.Electricity => exposure == ZenCrystalExposure.Electricity,
        ZenCrystalTaskType.Radiation => exposure == ZenCrystalExposure.Radiation,
        _ => false,
    };

    private static bool PulseReady(ZenCrystalChamberComponent chamber, ZenCrystalResearchComponent sample)
    {
        if (sample.Stage != ZenCrystalStage.Structure || sample.Step >= sample.Tasks.Count)
            return false;
        var task = sample.Tasks[sample.Step];
        if (!MatchesTask(chamber.Exposure, task))
            return false;
        return task.Type == ZenCrystalTaskType.Temperature
            ? Math.Abs(sample.Temperature - task.Target) <= task.Tolerance
            : !chamber.DoseOverloaded && chamber.AppliedDose >= task.Target && chamber.AppliedDose <= task.Target + task.Tolerance;
    }

    private void FailedTask(Entity<ZenCrystalChamberComponent> ent, Entity<ZenCrystalResearchComponent> sample,
        string reason, float instability)
    {
        sample.Comp.Quality = Math.Max(0.1f, sample.Comp.Quality - 0.05f);
        var message = Loc.GetString(reason);
        Log(sample.Comp, message);
        ChangeInstability(ent, sample, instability);
        if (sample.Comp.Stage != ZenCrystalStage.Exhausted)
            ent.Comp.Status = message;
        _popup.PopupEntity(message, ent, PopupType.MediumCaution);
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

    private void OnEmitterOpened(Entity<ZenCrystalEmitterComponent> ent, ref BoundUIOpenedEvent args) =>
        UpdateEmitterUi(ent);

    private void OnEmitterShutdown(Entity<ZenCrystalEmitterComponent> ent, ref ComponentShutdown args) =>
        StopEmitter(ent, false);

    private void OnEmitterCommand(Entity<ZenCrystalEmitterComponent> ent, ref ZenCrystalEmitterCommandMessage args)
    {
        if (!this.IsPowered(ent, EntityManager) || !Transform(ent).Anchored)
            return;
        if (ent.Comp.Active)
            StopEmitter(ent, true);
        else
            StartEmitter(ent, args.Actor);
        UpdateEmitterUi(ent);
    }

    private void StartEmitter(Entity<ZenCrystalEmitterComponent> ent, EntityUid user)
    {
        if (!FindChamber(ent, out var chamber) ||
            !_interaction.InRangeUnobstructed(ent.Owner, chamber.Owner, ent.Comp.Range))
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

        if (ent.Comp.Exposure is ZenCrystalExposure.Electricity or ZenCrystalExposure.Cold && GetConsumable(ent) <= 0f)
        {
            ent.Comp.Status = Loc.GetString("zen-lab-resource-missing");
            return;
        }

        var containment = sample.Comp.Runaway && ent.Comp.Exposure == ZenCrystalExposure.Cold;
        if (sample.Comp.Runaway && !containment)
        {
            ent.Comp.Status = Loc.GetString("zen-lab-runaway-apparatus");
            return;
        }
        if (chamber.Comp.ActiveEmitter != null || (!containment && (sample.Comp.Runaway ||
            sample.Comp.Stage != ZenCrystalStage.Structure || sample.Comp.Step >= sample.Comp.Tasks.Count ||
            sample.Comp.ScannedStep != sample.Comp.Step)))
        {
            ent.Comp.Status = Loc.GetString("zen-lab-scan-required");
            return;
        }
        chamber.Comp.ActiveEmitter = ent.Owner;
        chamber.Comp.Exposure = ent.Comp.Exposure;
        chamber.Comp.AppliedDose = 0f;
        chamber.Comp.ExposureSeconds = 0f;
        chamber.Comp.DoseOverloaded = false;
        sample.Comp.PulseAttempts++;


        ent.Comp.Chamber = chamber.Owner;
        ent.Comp.ContainmentCooling = containment;
        ent.Comp.Operator = user;
        ent.Comp.Active = true;
        ent.Comp.Status = Loc.GetString("zen-lab-apparatus-running");
        SetApparatusLocks(ent, true);
        _ambient.SetAmbience(ent, true);
        Dirty(ent);
    }

    private void StopEmitter(EntityUid uid, bool evaluate)
    {
        if (!TryComp<ZenCrystalEmitterComponent>(uid, out var emitter))
            return;
        emitter.Active = false;
        emitter.Status = Loc.GetString(evaluate ? "zen-crystal-paused" : "zen-lab-apparatus-interrupted");
        SetApparatusLocks((uid, emitter), false);
        _ambient.SetAmbience(uid, false);
        if (TryComp<ZenCrystalChamberComponent>(emitter.Chamber, out var comp) && comp.ActiveEmitter == uid)
        {
            Entity<ZenCrystalChamberComponent> chamber = (emitter.Chamber!.Value, comp);
            comp.ActiveEmitter = null;
            if (TryCrystal(chamber, out var sample) && !sample.Comp.Runaway && !emitter.ContainmentCooling && sample.Comp.Stage == ZenCrystalStage.Structure &&
                comp.ExposureSeconds > 0f)
            {
                if (evaluate)
                {
                    FinishPulse(chamber, sample, emitter);
                    emitter.Status = comp.Status;
                }
                else
                    ChangeInstability(chamber, sample, 10f);
            }

            UpdateUi(chamber);
        }

        emitter.Chamber = null;
        emitter.ContainmentCooling = false;
        Dirty(uid, emitter);
        UpdateEmitterUi((uid, emitter));
    }

    private void FinishPulse(Entity<ZenCrystalChamberComponent> chamber,
        Entity<ZenCrystalResearchComponent> sample, ZenCrystalEmitterComponent emitter)
    {
        if (PulseReady(chamber.Comp, sample.Comp))
        {
            var incidents = sample.Comp.Incidents;
            ChangeInstability(chamber, sample, 8f);
            if (sample.Comp.Stage != ZenCrystalStage.Exhausted)
            {
                CompleteTask(chamber, sample, emitter.Operator is { } user && !Deleted(user) ? Name(user) : null);
                if (incidents != sample.Comp.Incidents || sample.Comp.Runaway)
                    chamber.Comp.Running = false;
            }
        }
        else
            FailedTask(chamber, sample, chamber.Comp.DoseOverloaded ? "zen-lab-pulse-overloaded" : "zen-lab-task-failed", 20f);
        UpdateVisuals(sample);
    }

    private void ChangeInstability(Entity<ZenCrystalChamberComponent> chamber,
        Entity<ZenCrystalResearchComponent> sample, float amount)
    {
        if (sample.Comp.Stage == ZenCrystalStage.Exhausted)
            return;
        sample.Comp.Instability = Math.Clamp(sample.Comp.Instability + amount, 0f, 100f);
        if (sample.Comp.Instability >= RunawayThreshold)
            BeginRunaway(sample);
    }

    private string Reaction(Entity<ZenCrystalChamberComponent> chamber, ZenCrystalResearchComponent sample)
    {
        if (chamber.Comp.ActiveEmitter == null || sample.Stage != ZenCrystalStage.Structure || sample.Step >= sample.Tasks.Count)
            return string.Empty;
        if (!MatchesTask(chamber.Comp.Exposure, sample.Tasks[sample.Step]))
            return Loc.GetString("zen-lab-reaction-chaotic");
        if (chamber.Comp.DoseOverloaded)
            return Loc.GetString("zen-lab-reaction-fracture");
        return Loc.GetString(PulseReady(chamber.Comp, sample) ? "zen-lab-reaction-stable" : "zen-lab-reaction-building");
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
        {
            coolant.Remaining = 0f;
            Dirty(item, coolant);
            return false;
        }

        coolant.Remaining -= spent;
        Dirty(item, coolant);
        return true;
    }

    private void SetApparatusLocks(Entity<ZenCrystalEmitterComponent> ent, bool locked)
    {
        if (!HasComp<ItemSlotsComponent>(ent))
            return;
        if (ent.Comp.Exposure == ZenCrystalExposure.Cold)
            _slots.SetLock(ent, CoolantSlot, locked);
        else if (ent.Comp.Exposure == ZenCrystalExposure.Electricity)
            _slots.SetLock(ent, CapacitorSlot, locked);
    }

    private float GetConsumable(Entity<ZenCrystalEmitterComponent> ent)
    {
        if (ent.Comp.Exposure == ZenCrystalExposure.Cold && _slots.TryGetSlot(ent, CoolantSlot, out var coolantSlot) &&
            TryComp<ZenCrystalCoolantComponent>(coolantSlot.Item, out var coolant))
            return coolant.Remaining;
        if (ent.Comp.Exposure == ZenCrystalExposure.Electricity &&
            _slots.TryGetSlot(ent, CapacitorSlot, out var slot) &&
            TryComp<Content.Server.Power.Components.BatteryComponent>(slot.Item, out var battery))
            return battery.CurrentCharge;
        return -1f;
    }

    private void UpdateLaboratory(float frameTime)
    {
        _emitterUiAccumulator += frameTime;
        var updateUi = _emitterUiAccumulator >= 0.25f;
        if (updateUi)
            _emitterUiAccumulator = 0f;
        var samples = EntityQueryEnumerator<ZenCrystalResearchComponent>();
        while (samples.MoveNext(out _, out var sampleData))
            sampleData.Temperature += (20f - sampleData.Temperature) * Math.Min(1f, frameTime * 0.006f);

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

                if (emitter.ContainmentCooling)
                {
                    if (!sample.Comp.Runaway)
                    {
                        StopEmitter(uid, false);
                        continue;
                    }
                    sample.Comp.Temperature = Math.Max(-40f, sample.Comp.Temperature - 2.5f * frameTime);
                }
                else
                {
                    if (sample.Comp.Runaway || sample.Comp.Stage != ZenCrystalStage.Structure || sample.Comp.Step >= sample.Comp.Tasks.Count)
                    {
                        StopEmitter(uid, false);
                        continue;
                    }
                    chamber.Comp.ExposureSeconds += frameTime;
                    if (emitter.Exposure is ZenCrystalExposure.Heat or ZenCrystalExposure.Cold)
                        sample.Comp.Temperature = Math.Clamp(sample.Comp.Temperature +
                            (emitter.Exposure == ZenCrystalExposure.Heat ? 2.5f : -2.5f) * frameTime, -40f, 120f);
                    else
                        chamber.Comp.AppliedDose += emitter.Power * frameTime;
                    var task = sample.Comp.Tasks[sample.Comp.Step];
                    if (!MatchesTask(emitter.Exposure, task))
                        ChangeInstability(chamber, sample, 6f * frameTime);
                    else
                    {
                        var overloaded = task.Type == ZenCrystalTaskType.Temperature
                            ? emitter.Exposure == ZenCrystalExposure.Heat
                                ? sample.Comp.Temperature > task.Target + task.Tolerance
                                : sample.Comp.Temperature < task.Target - task.Tolerance
                            : chamber.Comp.AppliedDose > task.Target + task.Tolerance;
                        if (overloaded)
                        {
                            var penalty = chamber.Comp.DoseOverloaded ? 8f * frameTime : 30f;
                            chamber.Comp.DoseOverloaded = true;
                            ChangeInstability(chamber, sample, penalty);
                        }
                    }
                }
                if (!emitter.Active || sample.Comp.Stage == ZenCrystalStage.Exhausted)
                    continue;
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
            Power = ent.Comp.Exposure is ZenCrystalExposure.Heat or ZenCrystalExposure.Cold ? 2.5f : ent.Comp.Power,
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
                state.HasSample = true;
                state.SampleTemperature = MathF.Round(sample.Comp.Temperature, 1);
                state.Instability = sample.Comp.Instability;
                state.Hazard = HazardStatus(sample.Comp);
                if (ent.Comp.Active)
                    state.Status = ent.Comp.ContainmentCooling ? Loc.GetString("zen-lab-containment-cooling") : sample.Comp.Stage == ZenCrystalStage.Structure
                        ? Reaction(chamber, sample.Comp)
                        : Loc.GetString("zen-lab-task-growing");
            }
        }

        state.Consumable = GetConsumable(ent);
        if (ent.Comp.Exposure == ZenCrystalExposure.Cold)
            state.ConsumableCapacity = 120f;
        else if (_slots.TryGetSlot(ent, CapacitorSlot, out var slot) &&
                 TryComp<Content.Server.Power.Components.BatteryComponent>(slot.Item, out var battery))
            state.ConsumableCapacity = battery.MaxCharge;
        _ui.SetUiState(ent.Owner, ZenCrystalUiKey.Emitter, state);
    }
}
