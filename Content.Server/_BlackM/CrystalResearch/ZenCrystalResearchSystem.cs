using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Power.EntitySystems;
using Content.Server.Research.Systems;
using Content.Server._BlackM.Economy.PriceDecay;
using Content.Shared._BlackM.CrystalResearch;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Placeable;
using Content.Shared.Popups;
using Content.Shared.Research.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.Server._BlackM.CrystalResearch;

public sealed class ZenCrystalResearchSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private ResearchSystem _research = default!;
    [Dependency] private ZenResearchSystem _zenResearch = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private DamageableSystem _damage = default!;

    private float _uiAccumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ZenCrystalResearchComponent, MapInitEvent>(OnCrystalInit);
        SubscribeLocalEvent<ZenCrystalResearchComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ZenCrystalChamberComponent, ItemPlacedEvent>(OnPlaced);
        SubscribeLocalEvent<ZenCrystalChamberComponent, ItemRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<ZenCrystalChamberComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ZenCrystalChamberComponent, InteractUsingEvent>(OnRefill);
        Subs.BuiEvents<ZenCrystalChamberComponent>(ZenCrystalUiKey.Chamber, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<ZenCrystalCommandMessage>(OnCommand);
        });
    }

    private void OnCrystalInit(Entity<ZenCrystalResearchComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Sequence.Count != 0)
            return;

        var available = Enum.GetValues<ZenCrystalExposure>().ToList();
        for (var i = 0; i < 3; i++)
        {
            var index = _random.Next(available.Count);
            ent.Comp.Sequence.Add(available[index]);
            available.RemoveAt(index);
            ent.Comp.Intensities.Add(_random.Next(1, 4));
        }

        ent.Comp.Medium = _random.Prob(0.5f) ? ZenGrowthMedium.Mineral : ZenGrowthMedium.Organic;
        ent.Comp.GrowthTemperature = _random.Pick(new[] { 0, 20, 40, 60 });
        UpdateVisuals(ent);
    }

    private void OnExamined(Entity<ZenCrystalResearchComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("zen-crystal-examine", ("stage", Loc.GetString(StageKey(ent.Comp.Stage)))));
    }

    private void OnPlaced(Entity<ZenCrystalChamberComponent> ent, ref ItemPlacedEvent args)
    {
        if (ent.Comp.Crystal == null && TryComp<ZenCrystalResearchComponent>(args.OtherEntity, out var crystal) &&
            (crystal.ActiveMachine == null || crystal.ActiveMachine == ent.Owner))
        {
            ent.Comp.Crystal = args.OtherEntity;
            crystal.ActiveMachine = ent.Owner;
            ent.Comp.Status = string.Empty;
        }
        UpdateUi(ent);
    }

    private void OnRemoved(Entity<ZenCrystalChamberComponent> ent, ref ItemRemovedEvent args)
    {
        if (ent.Comp.Crystal == args.OtherEntity)
            Release(ent);
        UpdateUi(ent);
    }

    private void OnShutdown(Entity<ZenCrystalChamberComponent> ent, ref ComponentShutdown args) => Release(ent);

    private void Release(Entity<ZenCrystalChamberComponent> ent)
    {
        if (TryComp<ZenCrystalResearchComponent>(ent.Comp.Crystal, out var crystal) && crystal.ActiveMachine == ent.Owner)
            crystal.ActiveMachine = null;
        ent.Comp.Crystal = null;
        ent.Comp.Operation = null;
        ent.Comp.Running = false;
        ent.Comp.Status = string.Empty;
    }

    private bool TryCrystal(Entity<ZenCrystalChamberComponent> ent, out Entity<ZenCrystalResearchComponent> sample)
    {
        sample = default;
        if (ent.Comp.Crystal is not { } uid || TerminatingOrDeleted(uid) ||
            !TryComp<ZenCrystalResearchComponent>(uid, out var crystal) || crystal.ActiveMachine != ent.Owner ||
            _containers.IsEntityInContainer(uid))
            return false;

        var machineCoords = _transform.GetMapCoordinates(ent.Owner);
        var crystalCoords = _transform.GetMapCoordinates(uid);
        if (machineCoords.MapId != crystalCoords.MapId ||
            (machineCoords.Position - crystalCoords.Position).LengthSquared() > 1f)
            return false;

        sample = (uid, crystal);
        return true;
    }

    private void OnRefill(Entity<ZenCrystalChamberComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<ZenGrowthMediumComponent>(args.Used, out var medium) || ent.Comp.Resonator)
            return;
        args.Handled = true;
        var charges = medium.Medium == ZenGrowthMedium.Mineral ? ent.Comp.MineralCharges : ent.Comp.OrganicCharges;
        if (charges >= ent.Comp.MediumCapacity)
        {
            _popup.PopupEntity(Loc.GetString("zen-crystal-medium-full"), ent, args.User);
            return;
        }
        if (medium.Medium == ZenGrowthMedium.Mineral)
            ent.Comp.MineralCharges++;
        else
            ent.Comp.OrganicCharges++;
        QueueDel(args.Used);
        UpdateUi(ent);
    }

    private void OnOpened(Entity<ZenCrystalChamberComponent> ent, ref BoundUIOpenedEvent args) => UpdateUi(ent);

    private void OnCommand(Entity<ZenCrystalChamberComponent> ent, ref ZenCrystalCommandMessage args)
    {
        if (!this.IsPowered(ent, EntityManager) || !Transform(ent).Anchored || !TryCrystal(ent, out var sample))
            return;

        var crystal = sample.Comp;
        if (args.Command == ZenCrystalCommand.Stop)
        {
            ent.Comp.Operation = null;
            ent.Comp.Running = false;
            ent.Comp.Status = Loc.GetString("zen-crystal-paused");
            UpdateUi(ent);
            return;
        }

        if (ent.Comp.Operation != null)
            return;

        if (args.Command == ZenCrystalCommand.SetTemperature && !ent.Comp.Resonator)
        {
            if (args.Value is >= -20 and <= 80 && args.Value % 5 == 0)
                ent.Comp.Temperature = args.Value;
            UpdateUi(ent);
            return;
        }

        if (ent.Comp.Resonator)
        {
            if (args.Command == ZenCrystalCommand.Generate && crystal.Stage == ZenCrystalStage.Researched &&
                _research.TryGetClientServer(ent, out _, out _) && args.Value is 0 or 1)
            {
                ent.Comp.Running = true;
                ent.Comp.Forced = args.Value == 1;
                ent.Comp.Status = Loc.GetString(ent.Comp.Forced ? "zen-crystal-generating-forced" : "zen-crystal-generating");
            }
            UpdateUi(ent);
            return;
        }

        switch (args.Command)
        {
            case ZenCrystalCommand.Scan when crystal.Stage is ZenCrystalStage.Structure or ZenCrystalStage.Growth:
                StartOperation(ent, args.Command, ent.Comp.ScanSeconds, args.Actor);
                break;
            case ZenCrystalCommand.Expose when crystal.Stage == ZenCrystalStage.Structure &&
                                               crystal.ScannedStep == crystal.Step &&
                                               args.Value is >= 1 and <= 3 && Enum.IsDefined(args.Exposure):
                ent.Comp.Exposure = args.Exposure;
                ent.Comp.Intensity = args.Value;
                StartOperation(ent, args.Command, ent.Comp.ExperimentSeconds, args.Actor);
                break;
            case ZenCrystalCommand.Stabilize when crystal.Instability > 0 && crystal.Stage == ZenCrystalStage.Structure:
                StartOperation(ent, args.Command, ent.Comp.StabilizeSeconds, args.Actor);
                break;
            case ZenCrystalCommand.StartGrowth when crystal.Stage == ZenCrystalStage.Growth &&
                                                   (!crystal.GrowthStarted || !NeedsService(ent, crystal)):
                if (!crystal.GrowthStarted)
                {
                    if (!ConsumeMedium(ent, crystal.Medium))
                        break;
                    crystal.GrowthStarted = true;
                    Log(crystal, Loc.GetString("zen-crystal-growth-started"), Name(args.Actor));
                }
                ent.Comp.Running = true;
                break;
            case ZenCrystalCommand.ServiceGrowth when crystal.Stage == ZenCrystalStage.Growth && NeedsService(ent, crystal):
                if (crystal.GrowthCheckpoints == 0 && !ConsumeMedium(ent, crystal.Medium))
                    break;
                StartOperation(ent, args.Command, 8f, args.Actor);
                break;
            case ZenCrystalCommand.Finalize when crystal.Stage == ZenCrystalStage.Mature:
                if (_zenResearch.CanAward(ent))
                    StartOperation(ent, args.Command, ent.Comp.ScanSeconds, args.Actor);
                else
                    ent.Comp.Status = Loc.GetString("zen-crystal-no-server");
                break;
        }
        UpdateUi(ent);
    }

    private bool ConsumeMedium(Entity<ZenCrystalChamberComponent> ent, ZenGrowthMedium medium)
    {
        if ((medium == ZenGrowthMedium.Mineral ? ent.Comp.MineralCharges : ent.Comp.OrganicCharges) <= 0)
        {
            ent.Comp.Status = Loc.GetString("zen-crystal-need-medium", ("medium", Loc.GetString(MediumKey(medium))));
            return false;
        }
        if (medium == ZenGrowthMedium.Mineral)
            ent.Comp.MineralCharges--;
        else
            ent.Comp.OrganicCharges--;
        return true;
    }

    private void StartOperation(Entity<ZenCrystalChamberComponent> ent, ZenCrystalCommand command, float seconds, EntityUid user)
    {
        ent.Comp.Running = false;
        ent.Comp.Operation = command;
        ent.Comp.OperationRemaining = seconds;
        ent.Comp.Operator = Name(user);
        ent.Comp.Status = Loc.GetString("zen-crystal-operation-" + command.ToString().ToLowerInvariant());
    }

    private static bool NeedsService(Entity<ZenCrystalChamberComponent> ent, ZenCrystalResearchComponent crystal)
    {
        return crystal.GrowthStarted && crystal.GrowthCheckpoints < 2 &&
               crystal.GrowthSeconds >= ent.Comp.GrowthDuration * (crystal.GrowthCheckpoints + 1) / 3f;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _uiAccumulator += frameTime;
        var updateUi = _uiAccumulator >= 1f;
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

            if (this.IsPowered(uid, EntityManager) && Transform(uid).Anchored)
            {
                if (chamber.Operation != null)
                {
                    chamber.OperationRemaining -= frameTime;
                    if (chamber.OperationRemaining <= 0f)
                        FinishOperation(ent, sample);
                }
                else if (chamber.Running && chamber.Resonator)
                    Generate(ent, sample, frameTime);
                else if (chamber.Running && sample.Comp.Stage == ZenCrystalStage.Growth)
                    Grow(ent, sample, frameTime);
            }

            if (updateUi)
            {
                UpdateUi(ent);
                UpdateVisuals(sample);
            }
        }
    }

    private void FinishOperation(Entity<ZenCrystalChamberComponent> ent, Entity<ZenCrystalResearchComponent> sample)
    {
        var command = ent.Comp.Operation;
        ent.Comp.Operation = null;
        var crystal = sample.Comp;
        string result;
        switch (command)
        {
            case ZenCrystalCommand.Scan:
                crystal.ScannedStep = crystal.Step;
                result = crystal.Stage == ZenCrystalStage.Structure && crystal.Step < crystal.Sequence.Count
                    ? Loc.GetString("zen-crystal-clue-" + crystal.Sequence[crystal.Step].ToString().ToLowerInvariant())
                    : GrowthClue(crystal);
                break;
            case ZenCrystalCommand.Expose:
                if (crystal.Step >= crystal.Sequence.Count || crystal.Step >= crystal.Intensities.Count)
                    return;
                if (ent.Comp.Exposure != crystal.Sequence[crystal.Step])
                {
                    crystal.Instability = Math.Min(100f, crystal.Instability + 25f);
                    result = Loc.GetString("zen-crystal-wrong-exposure");
                }
                else if (ent.Comp.Intensity != crystal.Intensities[crystal.Step])
                {
                    crystal.Instability = Math.Min(100f, crystal.Instability + 5f);
                    result = Loc.GetString(ent.Comp.Intensity < crystal.Intensities[crystal.Step]
                        ? "zen-crystal-increase-intensity" : "zen-crystal-decrease-intensity");
                }
                else
                {
                    crystal.Step++;
                    result = Loc.GetString("zen-crystal-layer-opened", ("step", crystal.Step));
                    if (crystal.Step == crystal.Sequence.Count)
                    {
                        crystal.Stage = ZenCrystalStage.Growth;
                        result += "\n" + GrowthClue(crystal);
                    }
                }
                if (crystal.Instability >= 100f)
                {
                    crystal.Instability = 40f;
                    crystal.Quality = Math.Max(0.5f, crystal.Quality - 0.1f);
                    _damage.TryChangeDamage(ent, new DamageSpecifier { DamageDict = new() { { "Heat", 5 } } });
                    _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/sparks4.ogg"), ent);
                    result += "\n" + Loc.GetString("zen-crystal-incident");
                }
                else if (crystal.Instability >= 75f)
                    result += "\n" + Loc.GetString("zen-crystal-instability-warning");
                break;
            case ZenCrystalCommand.Stabilize:
                crystal.Instability = Math.Max(0f, crystal.Instability - 35f);
                result = Loc.GetString("zen-crystal-stabilized");
                break;
            case ZenCrystalCommand.ServiceGrowth:
                crystal.GrowthCheckpoints++;
                ent.Comp.Running = true;
                result = Loc.GetString("zen-crystal-service-done");
                break;
            case ZenCrystalCommand.Finalize:
                if (crystal.BreakthroughAwarded || !_zenResearch.TryAward(ent))
                {
                    ent.Comp.Status = Loc.GetString("zen-crystal-no-server");
                    UpdateUi(ent);
                    return;
                }
                crystal.BreakthroughAwarded = true;
                crystal.Stage = ZenCrystalStage.Researched;
                crystal.EnergyCapacity *= crystal.Quality;
                crystal.Energy = crystal.EnergyCapacity;
                RemCompDeferred<PriceDecayComponent>(sample);
                result = Loc.GetString("zen-crystal-research-finished");
                break;
            default:
                return;
        }
        ent.Comp.Status = result;
        Log(crystal, result, ent.Comp.Operator);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/scan_finish.ogg"), ent);
        UpdateVisuals(sample);
        UpdateUi(ent);
    }

    private void Grow(Entity<ZenCrystalChamberComponent> ent, Entity<ZenCrystalResearchComponent> sample, float frameTime)
    {
        var crystal = sample.Comp;
        if (NeedsService(ent, crystal))
        {
            ent.Comp.Running = false;
            ent.Comp.Status = Loc.GetString(crystal.GrowthCheckpoints == 0
                ? "zen-crystal-service-medium" : "zen-crystal-service-deposit");
            _popup.PopupEntity(ent.Comp.Status, ent, PopupType.Medium);
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/scan_finish.ogg"), ent);
            return;
        }
        if (Math.Abs(ent.Comp.Temperature - crystal.GrowthTemperature) > 5)
        {
            ent.Comp.Status = Loc.GetString("zen-crystal-temperature-paused");
            return;
        }
        ent.Comp.Status = Loc.GetString("zen-crystal-growing");
        var nextBoundary = ent.Comp.GrowthDuration * Math.Min(3, crystal.GrowthCheckpoints + 1) / 3f;
        crystal.GrowthSeconds = Math.Min(nextBoundary, crystal.GrowthSeconds + frameTime);
        if (crystal.GrowthSeconds >= ent.Comp.GrowthDuration)
        {
            crystal.Stage = ZenCrystalStage.Mature;
            ent.Comp.Running = false;
            ent.Comp.Status = Loc.GetString("zen-crystal-mature");
            Log(crystal, ent.Comp.Status);
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/scan_finish.ogg"), ent);
        }
    }

    private void Generate(Entity<ZenCrystalChamberComponent> ent, Entity<ZenCrystalResearchComponent> sample, float frameTime)
    {
        if (sample.Comp.Stage != ZenCrystalStage.Researched ||
            !_research.TryGetClientServer(ent, out var server, out var serverComp) ||
            !this.IsPowered(server.Value, EntityManager))
            return;

        // Energy is spent on the sample, so moving it never refills its research yield.
        var spent = Math.Min(sample.Comp.Energy, frameTime * (ent.Comp.Forced ? 2.5f : 1f));
        sample.Comp.Energy -= spent;
        sample.Comp.ResearchRemainder += spent * (ent.Comp.Forced ? 6f : 8f);
        var points = (int) sample.Comp.ResearchRemainder;
        sample.Comp.ResearchRemainder -= points;
        _research.ModifyServerPoints(server.Value, points, serverComp);
        if (sample.Comp.Energy <= 0f)
        {
            sample.Comp.Stage = ZenCrystalStage.Exhausted;
            ent.Comp.Running = false;
            ent.Comp.Status = Loc.GetString("zen-crystal-exhausted");
            Log(sample.Comp, ent.Comp.Status);
        }
    }

    private static void Log(ZenCrystalResearchComponent crystal, string message, string? author = null)
    {
        crystal.Journal.Add(string.IsNullOrEmpty(author) ? message : Loc.GetString("zen-crystal-log", ("author", author), ("text", message)));
        if (crystal.Journal.Count > 40)
            crystal.Journal.RemoveAt(0);
    }

    private static string GrowthClue(ZenCrystalResearchComponent crystal) => Loc.GetString("zen-crystal-growth-clue",
        ("medium", Loc.GetString(MediumKey(crystal.Medium))), ("temperature", crystal.GrowthTemperature));

    private static string MediumKey(ZenGrowthMedium medium) => "zen-crystal-medium-" + medium.ToString().ToLowerInvariant();
    private static string StageKey(ZenCrystalStage stage) => "zen-crystal-stage-" + stage.ToString().ToLowerInvariant();

    private void UpdateVisuals(Entity<ZenCrystalResearchComponent> sample)
    {
        var visuals = EnsureComp<ZenCrystalVisualsComponent>(sample);
        visuals.Growth = Math.Clamp(sample.Comp.GrowthSeconds / 210f, 0f, 1f);
        visuals.Researched = sample.Comp.BreakthroughAwarded;
        visuals.Energy = visuals.Researched ? Math.Clamp(sample.Comp.Energy / sample.Comp.EnergyCapacity, 0f, 1f) : 1f;
        Dirty(sample.Owner, visuals);
        _lights.SetEnergy(sample, 0.1f + 2.9f * visuals.Energy);
    }

    private void UpdateUi(Entity<ZenCrystalChamberComponent> ent)
    {
        var state = new ZenCrystalChamberState
        {
            Powered = this.IsPowered(ent, EntityManager) && Transform(ent).Anchored,
            Connected = _research.TryGetClientServer(ent, out _, out _),
            Resonator = ent.Comp.Resonator,
            Running = ent.Comp.Running,
            Forced = ent.Comp.Forced,
            Temperature = ent.Comp.Temperature,
            MineralCharges = ent.Comp.MineralCharges,
            OrganicCharges = ent.Comp.OrganicCharges,
            Operation = ent.Comp.Operation,
            RemainingSeconds = (int) Math.Ceiling(Math.Max(0f, ent.Comp.OperationRemaining)),
            Status = ent.Comp.Status,
        };
        if (TryCrystal(ent, out var sample))
        {
            state.HasCrystal = true;
            state.Name = Name(sample);
            state.Stage = sample.Comp.Stage;
            state.Step = sample.Comp.Step;
            state.Scanned = sample.Comp.ScannedStep == sample.Comp.Step;
            state.Instability = sample.Comp.Instability;
            state.Quality = sample.Comp.Quality;
            state.Growth = Math.Clamp(sample.Comp.GrowthSeconds / ent.Comp.GrowthDuration, 0f, 1f);
            state.Energy = sample.Comp.Energy;
            state.EnergyCapacity = sample.Comp.EnergyCapacity;
            state.GrowthStarted = sample.Comp.GrowthStarted;
            state.NeedsService = NeedsService(ent, sample.Comp);
            state.Journal = new List<string>(sample.Comp.Journal);
        }
        _ui.SetUiState(ent.Owner, ZenCrystalUiKey.Chamber, state);
    }
}
