using System.Linq;
using Content.Server.Cargo.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Research.Systems;
using Content.Server.Stack;
using Content.Server._BlackM.Economy.PriceDecay;
using Content.Shared._BlackM.CrystalResearch;
using Content.Shared.Audio;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Xenoarchaeology.Equipment;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.Server._BlackM.CrystalResearch;

public sealed partial class ZenCrystalResearchSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private ResearchSystem _research = default!;
    [Dependency] private ZenResearchSystem _zenResearch = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedItemSystem _items = default!;
    [Dependency] private StackSystem _stack = default!;

    private float _uiAccumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ZenCrystalResearchComponent, MapInitEvent>(OnCrystalInit);
        SubscribeLocalEvent<ZenCrystalResearchComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ZenCrystalSpentComponent, ArtifactCrusherOutputEvent>(OnCrystalCrushed);
        SubscribeLocalEvent<ZenCrystalChamberComponent, EntInsertedIntoContainerMessage>(OnSampleInserted);
        SubscribeLocalEvent<ZenCrystalChamberComponent, EntRemovedFromContainerMessage>(OnSampleRemoved);
        SubscribeLocalEvent<ZenCrystalChamberComponent, ContainerIsRemovingAttemptEvent>(OnChamberRemoveAttempt);
        SubscribeLocalEvent<ZenCrystalChamberComponent, ContainerIsInsertingAttemptEvent>(OnChamberInsertAttempt);
        SubscribeLocalEvent<ZenCrystalChamberComponent, ComponentShutdown>(OnShutdown);
        Subs.BuiEvents<ZenCrystalChamberComponent>(ZenCrystalUiKey.Chamber,
            subs =>
            {
                subs.Event<BoundUIOpenedEvent>(OnOpened);
                subs.Event<ZenCrystalCommandMessage>(OnCommand);
            });
        InitializeLaboratory();
    }

    private void OnCrystalInit(Entity<ZenCrystalResearchComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Tasks.Count == 0 && ent.Comp.Stage is not (ZenCrystalStage.Researched or ZenCrystalStage.Exhausted))
        {
            var count = _random.Next(5, 8);
            var tasks = new List<ZenCrystalTaskType>
            {
                _random.Pick(new[] { ZenCrystalTaskType.Temperature, ZenCrystalTaskType.Electricity, ZenCrystalTaskType.Radiation }),
            };
            while (tasks.Count < count)
                tasks.Add(_random.Pick(new[] { ZenCrystalTaskType.Temperature, ZenCrystalTaskType.Electricity,
                    ZenCrystalTaskType.Radiation, ZenCrystalTaskType.Medium, ZenCrystalTaskType.Scrape }));
            var previousTemperature = 20f;
            while (tasks.Count > 0)
            {
                var index = _random.Next(tasks.Count);
                var type = tasks[index];
                tasks.RemoveAt(index);
                var temperature = _random.Pick(new[] { -10f, 0f, 10f, 35f, 45f, 60f, 75f }
                    .Where(value => Math.Abs(value - previousTemperature) > 4f).ToArray());
                if (type == ZenCrystalTaskType.Temperature)
                    previousTemperature = temperature;
                ent.Comp.Tasks.Add(new ZenCrystalResearchTask
                {
                    Type = type,
                    Target = type == ZenCrystalTaskType.Temperature
                        ? temperature
                        : _random.Next(30, 91),
                    Tolerance = type == ZenCrystalTaskType.Temperature ? 2f : _random.Next(6, 13),
                    Medium = _random.Prob(0.5f) ? ZenGrowthMedium.Mineral : ZenGrowthMedium.Organic,
                });
            }
            ent.Comp.CurrentTask = Loc.GetString("zen-lab-task-scan");
        }
        if (ent.Comp.Stage == ZenCrystalStage.Exhausted)
            EnsureComp<ZenCrystalSpentComponent>(ent);
        UpdateVisuals(ent);
        if (ent.Comp.Stage is ZenCrystalStage.Mature or ZenCrystalStage.Researched or ZenCrystalStage.Exhausted)
            _items.SetSize(ent, "Large");
    }

    private void OnExamined(Entity<ZenCrystalResearchComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("zen-crystal-examine", ("stage", Loc.GetString(StageKey(ent.Comp.Stage)))));
        if (ent.Comp.Runaway)
            args.PushText(HazardStatus(ent.Comp));
    }

    private void OnCrystalCrushed(Entity<ZenCrystalSpentComponent> ent, ref ArtifactCrusherOutputEvent args)
    {
        if (!TryComp<ZenCrystalResearchComponent>(ent, out var sample) || sample.Stage != ZenCrystalStage.Exhausted)
            return;
        args.Handled = true;
        args.Output.AddRange(_stack.SpawnMultiple("Glass", sample.BreakthroughAwarded ? 3 : 1, args.Coordinates));
        if (sample.BreakthroughAwarded)
            args.Output.AddRange(_stack.SpawnMultiple("Gold", 1, args.Coordinates));
    }

    private void OnSampleInserted(Entity<ZenCrystalChamberComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == MediumSlot)
        {
            UpdateUi(ent);
            return;
        }
        if (args.Container.ID != SampleSlot ||
            !TryComp<ZenCrystalResearchComponent>(args.Entity, out var crystal))
            return;
        ent.Comp.Crystal = args.Entity;
        crystal.ActiveMachine = ent.Owner;
        ent.Comp.Status = crystal.Stage == ZenCrystalStage.Structure && crystal.ScannedStep != crystal.Step
            ? Loc.GetString("zen-lab-task-scan") : string.Empty;
        UpdateVisuals((args.Entity, crystal));
        UpdateUi(ent);
    }

    private void OnChamberRemoveAttempt(Entity<ZenCrystalChamberComponent> ent, ref ContainerIsRemovingAttemptEvent args)
    {
        if (!ent.Comp.Resonator && !ent.Comp.LidOpen && args.Container.ID is SampleSlot or MediumSlot)
            args.Cancel();
    }

    private void OnChamberInsertAttempt(Entity<ZenCrystalChamberComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (!ent.Comp.Resonator && !ent.Comp.LidOpen && args.Container.ID is SampleSlot or MediumSlot)
            args.Cancel();
    }

    private void OnSampleRemoved(Entity<ZenCrystalChamberComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != SampleSlot)
            return;
        if (ent.Comp.Crystal == args.Entity)
            Release(ent);
        UpdateUi(ent);
    }

    private void OnShutdown(Entity<ZenCrystalChamberComponent> ent, ref ComponentShutdown args) => Release(ent);

    private void Release(Entity<ZenCrystalChamberComponent> ent)
    {
        if (ent.Comp.ActiveEmitter is { } emitter)
            StopEmitter(emitter, false);
        if (TryComp<ZenCrystalResearchComponent>(ent.Comp.Crystal, out var crystal) &&
            crystal.ActiveMachine == ent.Owner)
        {
            crystal.ActiveMachine = null;
            if (!TerminatingOrDeleted(ent.Comp.Crystal!.Value))
                UpdateVisuals((ent.Comp.Crystal.Value, crystal));
        }
        ent.Comp.Crystal = null;
        ent.Comp.Operation = null;
        ent.Comp.Running = false;
        ent.Comp.AppliedDose = 0f;
        ent.Comp.ExposureSeconds = 0f;
        ent.Comp.Status = string.Empty;
        _ambient.SetAmbience(ent, false);
    }

    private bool TryCrystal(Entity<ZenCrystalChamberComponent> ent, out Entity<ZenCrystalResearchComponent> sample)
    {
        sample = default;
        if (!_slots.TryGetSlot(ent, SampleSlot, out var slot) || slot.Item is not { } uid ||
            TerminatingOrDeleted(uid) || !TryComp<ZenCrystalResearchComponent>(uid, out var crystal))
            return false;
        sample = (uid, crystal);
        return true;
    }

    private void OnOpened(Entity<ZenCrystalChamberComponent> ent, ref BoundUIOpenedEvent args) => UpdateUi(ent);

    private void OnCommand(Entity<ZenCrystalChamberComponent> ent, ref ZenCrystalCommandMessage args)
    {
        if (args.Command == ZenCrystalCommand.Quench)
        {
            if (!ent.Comp.Resonator && _interaction.InRangeUnobstructed(args.Actor, ent.Owner) &&
                TryCrystal(ent, out var unstable) && args.Sample == GetNetEntity(unstable.Owner))
                Quench(ent, unstable, args.Actor);
            return;
        }
        if (!this.IsPowered(ent, EntityManager) || !Transform(ent).Anchored || !TryCrystal(ent, out var sample))
            return;
        if (args.Command == ZenCrystalCommand.Stop)
        {
            if (ent.Comp.ActiveEmitter != null)
                return;
            ent.Comp.Operation = null;
            ent.Comp.Running = false;
            ent.Comp.Status = Loc.GetString("zen-crystal-paused");
            UpdateUi(ent);
            return;
        }

        if (ent.Comp.Operation != null || ent.Comp.ActiveEmitter != null)
            return;
        if (ent.Comp.Resonator)
        {
            if (args.Command == ZenCrystalCommand.Generate && sample.Comp.Stage == ZenCrystalStage.Researched &&
                _zenResearch.CanAward(ent) && args.Value is 0 or 1)
            {
                ent.Comp.Running = true;
                ent.Comp.Forced = args.Value == 1;
            }

            UpdateUi(ent);
            return;
        }

        if (sample.Comp.Runaway)
            return;
        // Physical tools and linked apparatus own scanning, exposure, stabilization and servicing.
        switch (args.Command)
        {
            case ZenCrystalCommand.StartGrowth when sample.Comp.Stage == ZenCrystalStage.Growth && !ent.Comp.LidOpen:
                sample.Comp.AcceleratedGrowth = false;
                ent.Comp.Running = true;
                break;

            case ZenCrystalCommand.AccelerateGrowth when sample.Comp.Stage == ZenCrystalStage.Growth && !ent.Comp.LidOpen:
                sample.Comp.AcceleratedGrowth = true;
                ent.Comp.Running = true;
                Log(sample.Comp, Loc.GetString("zen-lab-accelerated-started"), Name(args.Actor));
                break;

            case ZenCrystalCommand.Finalize when sample.Comp.Stage == ZenCrystalStage.Mature && !ent.Comp.LidOpen:
                if (_zenResearch.CanAward(ent))
                {
                    ent.Comp.Operation = args.Command;
                    ent.Comp.OperationRemaining = ent.Comp.ScanSeconds;
                    ent.Comp.Operator = Name(args.Actor);
                    ent.Comp.Status = Loc.GetString("zen-crystal-operation-finalize");
                }
                else
                    ent.Comp.Status = Loc.GetString("zen-crystal-no-server");

                break;
        }

        UpdateUi(ent);
    }

    private static bool NeedsService(ZenCrystalResearchComponent sample) =>
        sample.Stage == ZenCrystalStage.Structure && sample.Step < sample.Tasks.Count &&
        sample.Tasks[sample.Step].Type is ZenCrystalTaskType.Medium or ZenCrystalTaskType.Scrape;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateLaboratory(frameTime);
        UpdateHazards(frameTime);
        _uiAccumulator += frameTime;
        var updateUi = _uiAccumulator >= 0.25f;
        if (updateUi)
            _uiAccumulator = 0f;
        var query = EntityQueryEnumerator<ZenCrystalChamberComponent>();
        while (query.MoveNext(out var uid, out var chamber))
        {
            Entity<ZenCrystalChamberComponent> ent = (uid, chamber);
            if (!TryCrystal(ent, out var sample))
            {
                if (chamber.Crystal != null)
                    Release(ent);
                if (updateUi)
                    UpdateUi(ent);
                continue;
            }

            chamber.Crystal = sample.Owner;
            sample.Comp.ActiveMachine = uid;
            var powered = this.IsPowered(uid, EntityManager) && Transform(uid).Anchored;
            if (powered)
            {
                if (chamber.Operation == ZenCrystalCommand.Finalize && !chamber.LidOpen && !sample.Comp.Runaway)
                {
                    chamber.OperationRemaining -= frameTime;
                    if (chamber.OperationRemaining <= 0f)
                        FinishResearch(ent, sample);
                }
                else if (chamber.Running && chamber.Resonator)
                    Generate(ent, sample, frameTime);
                else if (chamber.Running && sample.Comp.Stage == ZenCrystalStage.Growth)
                    Grow(ent, sample, frameTime);
            }

            _ambient.SetAmbience(uid,
                !sample.Comp.Runaway && powered && !chamber.LidOpen &&
                (chamber.Running || chamber.ActiveEmitter != null || chamber.Operation != null));
            if (updateUi)
            {
                UpdateUi(ent);
                UpdateVisuals(sample);
            }
        }
    }

    private void FinishResearch(Entity<ZenCrystalChamberComponent> ent, Entity<ZenCrystalResearchComponent> sample)
    {
        ent.Comp.Operation = null;
        var reward = HasGrowthBonus(sample.Comp) ? 2 : 1;
        if (sample.Comp.Stage != ZenCrystalStage.Mature || sample.Comp.BreakthroughAwarded ||
            sample.Comp.Runaway || !_zenResearch.TryAward(ent, reward))
            return;
        sample.Comp.BreakthroughAwarded = true;
        sample.Comp.Instability = 0f;
        sample.Comp.Stage = ZenCrystalStage.Researched;
        sample.Comp.CurrentTask = Loc.GetString("zen-lab-task-resonate");
        sample.Comp.EnergyCapacity *= sample.Comp.Quality;
        sample.Comp.Energy = sample.Comp.EnergyCapacity;
        RemCompDeferred<PriceDecayComponent>(sample);
        if (TryComp<StaticPriceComponent>(sample, out var price))
            price.Price = 100;
        ent.Comp.Status = Loc.GetString(reward > 1 ? "zen-lab-research-bonus-finished" : "zen-crystal-research-finished");
        Log(sample.Comp, ent.Comp.Status, ent.Comp.Operator);
        Spawn("ZenCrystalResearchPulse", Transform(sample).Coordinates);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/scan_finish.ogg"), ent);
        UpdateUi(ent);
    }

    private void Grow(Entity<ZenCrystalChamberComponent> ent,
        Entity<ZenCrystalResearchComponent> sample, float frameTime)
    {
        if (ent.Comp.LidOpen || sample.Comp.Runaway)
        {
            ent.Comp.Running = false;
            return;
        }
        var boundary = sample.Comp.GrowthDuration * sample.Comp.Step / Math.Max(1, sample.Comp.Tasks.Count);
        var accelerated = sample.Comp.AcceleratedGrowth;
        var advanced = Math.Clamp(boundary - sample.Comp.GrowthSeconds, 0f, frameTime * (accelerated ? 2f : 1f));
        sample.Comp.GrowthSeconds += advanced;
        if (accelerated)
        {
            sample.Comp.AcceleratedGrowthSeconds += advanced;
            sample.Comp.Temperature += advanced * 0.2f;
        }
        ChangeInstability(ent, sample, advanced * (accelerated ? 0.45f : 0.08f));
        if (sample.Comp.Stage == ZenCrystalStage.Exhausted || !ent.Comp.Running)
            return;
        ent.Comp.Status = Loc.GetString(accelerated ? "zen-lab-accelerated-growing" : "zen-crystal-growing");
        ent.Comp.EffectAccumulator += frameTime;
        if (ent.Comp.EffectAccumulator >= 3f)
        {
            ent.Comp.EffectAccumulator = 0f;
            Spawn("ZenCrystalGrowthPulse", Transform(ent).Coordinates);
        }
        if (sample.Comp.GrowthSeconds + 0.001f < boundary)
            return;
        ent.Comp.Running = false;
        if (sample.Comp.Step >= sample.Comp.Tasks.Count)
        {
            sample.Comp.Stage = ZenCrystalStage.Mature;
            _items.SetSize(sample, "Large");
            sample.Comp.CurrentTask = Loc.GetString("zen-lab-task-finalize");
            ent.Comp.Status = Loc.GetString("zen-crystal-mature");
        }
        else
        {
            sample.Comp.Stage = ZenCrystalStage.Structure;
            sample.Comp.ScannedStep = -1;
            ent.Comp.AppliedDose = 0f;
            ent.Comp.ExposureSeconds = 0f;
            ent.Comp.DoseOverloaded = false;
            sample.Comp.CurrentTask = Loc.GetString("zen-lab-task-scan");
            ent.Comp.Status = sample.Comp.CurrentTask;
        }
        Log(sample.Comp, ent.Comp.Status);
        _popup.PopupEntity(ent.Comp.Status, ent, PopupType.Medium);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/scan_finish.ogg"), ent);
    }

    private void Generate(Entity<ZenCrystalChamberComponent> ent,
        Entity<ZenCrystalResearchComponent> sample,
        float frameTime)
    {
        if (sample.Comp.Stage != ZenCrystalStage.Researched || !_zenResearch.CanAward(ent) ||
            !_research.TryGetClientServer(ent, out var server, out var serverComp))
            return;
        var spent = Math.Min(sample.Comp.Energy, frameTime * (ent.Comp.Forced ? 2.5f : 1f));
        sample.Comp.Energy -= spent;
        sample.Comp.ResearchRemainder += spent * (ent.Comp.Forced ? 6f : 8f);
        ent.Comp.GenerationAccumulator += frameTime;
        if (ent.Comp.GenerationAccumulator >= 1f || sample.Comp.Energy <= 0f)
        {
            ent.Comp.GenerationAccumulator = 0f;
            var points = (int) sample.Comp.ResearchRemainder;
            sample.Comp.ResearchRemainder -= points;
            _research.ModifyServerPoints(server.Value, points, serverComp);
        }

        if (sample.Comp.Energy <= 0f)
        {
            ExhaustSample(sample, ent, "zen-crystal-exhausted");
        }
    }

    private void Log(ZenCrystalResearchComponent crystal, string message, string? author = null)
    {
        crystal.Journal.Add(string.IsNullOrEmpty(author)
            ? message
            : Loc.GetString("zen-crystal-log", ("author", author), ("text", message)));
        if (crystal.Journal.Count > 60)
            crystal.Journal.RemoveAt(0);
    }

    private void CompleteTask(Entity<ZenCrystalChamberComponent> ent,
        Entity<ZenCrystalResearchComponent> sample, string? author = null)
    {
        sample.Comp.Step++;
        sample.Comp.PulseAttempts = 0;
        sample.Comp.Stage = ZenCrystalStage.Growth;
        sample.Comp.AcceleratedGrowth = false;
        if (!sample.Comp.GrowthStarted)
            sample.Comp.GrowthDuration = Math.Max(1f, ent.Comp.GrowthDuration);
        sample.Comp.GrowthStarted = true;
        sample.Comp.CurrentTask = Loc.GetString("zen-lab-task-growing");
        ent.Comp.Running = !ent.Comp.LidOpen;
        ent.Comp.Status = Loc.GetString("zen-lab-task-completed", ("step", sample.Comp.Step));
        Log(sample.Comp, ent.Comp.Status, author);
        Spawn("ZenCrystalResearchPulse", Transform(ent).Coordinates);
        UpdateVisuals(sample);
        UpdateUi(ent);
    }

    private void ExhaustSample(Entity<ZenCrystalResearchComponent> sample,
        Entity<ZenCrystalChamberComponent> ent, string reason)
    {
        sample.Comp.Stage = ZenCrystalStage.Exhausted;
        sample.Comp.Runaway = false;
        sample.Comp.BreachRemaining = 0f;
        sample.Comp.AcceleratedGrowth = false;
        sample.Comp.Energy = 0f;
        sample.Comp.CurrentTask = Loc.GetString("zen-lab-task-recycle");
        EnsureComp<ZenCrystalSpentComponent>(sample);
        ent.Comp.Running = false;
        ent.Comp.Operation = null;
        ent.Comp.Status = Loc.GetString(reason);
        Log(sample.Comp, ent.Comp.Status);
        if (ent.Comp.ActiveEmitter is { } emitter)
            StopEmitter(emitter, false);
        UpdateVisuals(sample);
    }

    private static string MediumKey(ZenGrowthMedium medium) =>
        "zen-crystal-medium-" + medium.ToString().ToLowerInvariant();

    private static string StageKey(ZenCrystalStage stage) => "zen-crystal-stage-" + stage.ToString().ToLowerInvariant();

    private void UpdateVisuals(Entity<ZenCrystalResearchComponent> sample)
    {
        var visuals = EnsureComp<ZenCrystalVisualsComponent>(sample);
        visuals.Growth = Math.Clamp(sample.Comp.GrowthSeconds / Math.Max(1f, sample.Comp.GrowthDuration), 0f, 1f);
        visuals.Researched = sample.Comp.BreakthroughAwarded || sample.Comp.Stage == ZenCrystalStage.Exhausted;
        visuals.Energy = visuals.Researched
            ? Math.Clamp(sample.Comp.Energy / Math.Max(1f, sample.Comp.EnergyCapacity), 0f, 1f)
            : 1f;
        visuals.Instability = sample.Comp.Instability / 100f;
        visuals.Active = sample.Comp.Runaway;
        visuals.Overloaded = sample.Comp.Runaway;
        visuals.Exposure = ZenCrystalExposure.Heat;
        visuals.Deposits = sample.Comp.Stage == ZenCrystalStage.Structure &&
            sample.Comp.Step < sample.Comp.Tasks.Count && sample.Comp.Tasks[sample.Comp.Step].Type == ZenCrystalTaskType.Scrape;
        if (TryComp<ZenCrystalChamberComponent>(sample.Comp.ActiveMachine, out var chamber) && chamber.ActiveEmitter != null)
        {
            visuals.Active = true;
            visuals.Overloaded = sample.Comp.Runaway || chamber.DoseOverloaded;
            visuals.Exposure = chamber.Exposure;
            visuals.Reaction = PulseReady(chamber, sample.Comp) ? 1f : 0f;
        }

        Dirty(sample.Owner, visuals);
        _lights.SetEnergy(sample, sample.Comp.ActiveMachine == null ? 0.2f + visuals.Energy * 0.6f : 0f);
        _lights.SetColor(sample,
            visuals.Instability >= 0.75f ? Color.FromHex("#E37666") :
            visuals.Active ? visuals.Exposure switch
            {
                ZenCrystalExposure.Heat => Color.FromHex("#E4A16B"),
                ZenCrystalExposure.Cold => Color.FromHex("#85CDDD"),
                ZenCrystalExposure.Electricity => Color.FromHex("#D5DFFF"),
                _ => Color.FromHex("#A6D877"),
            } : Color.FromHex("#FFFF00"));
    }

    private ZenCrystalChamberState SampleState(Entity<ZenCrystalResearchComponent> sample)
    {
        return new ZenCrystalChamberState
        {
            HasCrystal = true,
            Sample = GetNetEntity(sample.Owner),
            Name = Name(sample),
            Stage = sample.Comp.Stage,
            Step = sample.Comp.Step,
            Tasks = sample.Comp.Tasks.Count,
            Scanned = sample.Comp.ScannedStep == sample.Comp.Step,
            Instability = sample.Comp.Instability,
            Quality = sample.Comp.Quality,
            Growth = Math.Clamp(sample.Comp.GrowthSeconds / Math.Max(1f, sample.Comp.GrowthDuration), 0f, 1f),
            Energy = sample.Comp.Energy,
            EnergyCapacity = sample.Comp.EnergyCapacity,
            GrowthStarted = sample.Comp.GrowthStarted,
            AcceleratedGrowth = sample.Comp.AcceleratedGrowth,
            BonusProgress = Math.Clamp(sample.Comp.AcceleratedGrowthSeconds / Math.Max(1f, sample.Comp.GrowthDuration * 0.5f), 0f, 1f),
            Runaway = sample.Comp.Runaway,
            Hazard = HazardStatus(sample.Comp),
            NeedsService = NeedsService(sample.Comp),
        };
    }

    private void UpdateUi(Entity<ZenCrystalChamberComponent> ent)
    {
        _appearance.SetData(ent, ZenCrystalChamberVisuals.LidOpen, ent.Comp.LidOpen);
        _appearance.SetData(ent, ZenCrystalChamberVisuals.Active,
            this.IsPowered(ent, EntityManager) && (ent.Comp.Running || ent.Comp.ActiveEmitter != null || ent.Comp.Operation != null));
        var hasSample = TryCrystal(ent, out var sample);
        _appearance.SetData(ent, ZenCrystalChamberVisuals.Alarm, hasSample && sample.Comp.Runaway);
        _appearance.SetData(ent, ZenCrystalChamberVisuals.Cracks,
            !hasSample || ent.Comp.LidOpen || sample.Comp.Stage == ZenCrystalStage.Exhausted ||
            (!sample.Comp.Runaway && sample.Comp.Instability < 65f) ? "none" :
            sample.Comp.GrowthSeconds >= sample.Comp.GrowthDuration * 0.99f ? "mature" :
            sample.Comp.GrowthSeconds >= sample.Comp.GrowthDuration / 3f ? "growing" : "seed");
        _appearance.SetData(ent, ZenCrystalChamberVisuals.Sample, !hasSample || (!ent.Comp.Resonator && ent.Comp.LidOpen) ? "none" :
            sample.Comp.Stage == ZenCrystalStage.Exhausted ? "exhausted" :
            sample.Comp.BreakthroughAwarded && sample.Comp.Energy < sample.Comp.EnergyCapacity * 0.5f ? "dim" :
            sample.Comp.GrowthSeconds >= sample.Comp.GrowthDuration * 0.99f ? "mature" :
            sample.Comp.GrowthSeconds >= sample.Comp.GrowthDuration / 3f ? "growing" : "seed");
        _appearance.SetData(ent, ZenCrystalChamberVisuals.Deposits, hasSample &&
            sample.Comp.Stage == ZenCrystalStage.Structure && sample.Comp.Step < sample.Comp.Tasks.Count &&
            sample.Comp.Tasks[sample.Comp.Step].Type == ZenCrystalTaskType.Scrape);
        var state = hasSample ? SampleState(sample) : new ZenCrystalChamberState();
        state.Powered = this.IsPowered(ent, EntityManager) && Transform(ent).Anchored;
        state.Connected = _zenResearch.CanAward(ent);
        state.Resonator = ent.Comp.Resonator;
        state.Running = ent.Comp.Running;
        state.Forced = ent.Comp.Forced;
        state.Temperature = hasSample ? MathF.Round(sample.Comp.Temperature, 1) : 20f;
        state.LidOpen = ent.Comp.LidOpen;
        state.Operation = ent.Comp.Operation;
        state.RemainingSeconds = (int) Math.Ceiling(Math.Max(0f, ent.Comp.OperationRemaining));
        state.Status = ent.Comp.Status;
        state.Exposing = ent.Comp.ActiveEmitter != null;
        state.Dose = ent.Comp.AppliedDose;
        state.Reaction = hasSample ? Reaction(ent, sample.Comp) : string.Empty;
        if (TryGetGrowthMedium(ent, out var medium))
        {
            state.MediumPresent = true;
            state.MediumSpent = medium.Comp.Spent;
            state.MediumName = Loc.GetString(MediumKey(medium.Comp.Medium));
        }

        _ui.SetUiState(ent.Owner, ZenCrystalUiKey.Chamber, state);
    }
}
