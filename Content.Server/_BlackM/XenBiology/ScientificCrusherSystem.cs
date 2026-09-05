using Content.Server.Electrocution;
using Content.Server.Power.EntitySystems;
using Content.Server.Research.Systems;
using Content.Server.Stack;
using Content.Shared._BlackM.XenBiology;
using Content.Shared.Damage;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Research.Components;
using Content.Shared.Storage.Components;
using Content.Shared.StatusEffect;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.XenBiology;

public sealed class ScientificCrusherSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly ElectrocutionSystem _electrocution = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ResearchSystem _research = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ScientificCrusherComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<ScientificCrusherComponent, PowerChangedEvent>(OnPowerChanged);
    }

    private void OnGetVerbs(Entity<ScientificCrusherComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        if (!CanStartCrushing(ent, user, quiet: true))
            return;

        var verb = new AlternativeVerb
        {
            Text = Loc.GetString("scientific-crusher-verb-start"),
            Priority = 2,
            Act = () => TryStartCrushing(ent, user)
        };

        args.Verbs.Add(verb);
    }

    private void OnPowerChanged(Entity<ScientificCrusherComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered)
            StopCrushing(ent);
    }

    public bool TryStartCrushing(Entity<ScientificCrusherComponent> crusher, EntityUid user)
    {
        if (!CanStartCrushing(crusher, user))
            return false;

        DoStartCrushing(crusher);
        return true;
    }

    public bool CanStartCrushing(Entity<ScientificCrusherComponent> crusher, EntityUid user, bool quiet = false)
    {
        if (crusher.Comp.Crushing)
            return false;

        if (!this.IsPowered(crusher, EntityManager))
            return false;

        if (!TryComp<EntityStorageComponent>(crusher, out var storage) ||
            storage.Contents.ContainedEntities.Count == 0)
            return false;

        if (!TryComp<ResearchClientComponent>(crusher, out var client) ||
            !_research.TryGetClientServer(crusher, out _, out _, client))
        {
            if (!quiet)
                _popup.PopupEntity(Loc.GetString("scientific-crusher-no-server"), crusher, user);
            return false;
        }

        foreach (var contained in storage.Contents.ContainedEntities)
        {
            if (HasComp<ScientificCrusherInputComponent>(contained))
                return true;
        }

        if (!quiet)
            _popup.PopupEntity(Loc.GetString("scientific-crusher-no-valid-input"), crusher, user);

        return false;
    }

    private void DoStartCrushing(Entity<ScientificCrusherComponent> crusher)
    {
        crusher.Comp.Crushing = true;
        crusher.Comp.CrushEndTime = _timing.CurTime + crusher.Comp.CrushDuration;

        _appearance.SetData(crusher, ScientificCrusherVisuals.Crushing, true);
        Dirty(crusher);
    }

    private void StopCrushing(Entity<ScientificCrusherComponent> crusher)
    {
        if (!crusher.Comp.Crushing)
            return;

        crusher.Comp.Crushing = false;
        _appearance.SetData(crusher, ScientificCrusherVisuals.Crushing, false);
        Dirty(crusher);
    }

    private void FinishCrushing(
        Entity<ScientificCrusherComponent, EntityStorageComponent, ResearchClientComponent> crusher)
    {
        StopCrushing((crusher.Owner, crusher.Comp1));

        if (!_research.TryGetClientServer(crusher, out var server, out var serverComp, crusher.Comp3))
            return;

        var points = 0;
        var materialUnits = 0;
        var incidentChance = 0f;
        var contents = new List<EntityUid>(crusher.Comp2.Contents.ContainedEntities);
        var outputs = new List<CrusherOutput>();

        foreach (var contained in contents)
        {
            if (!TryComp<ScientificCrusherInputComponent>(contained, out var input))
                continue;

            var result = CompOrNull<XenExperimentResultComponent>(contained);
            var outputPoints = result?.RecordedData ?? input.Points;
            var outputMaterialUnits = result?.MaterialAmount ?? input.MaterialAmount;

            points += outputPoints;
            materialUnits += outputMaterialUnits;
            incidentChance += input.IncidentChance * GetIncidentChanceMultiplier(result);
            outputs.Add(new CrusherOutput(
                contained,
                input.MaterialStack,
                outputMaterialUnits,
                input.EmptyResult));
        }

        if (points <= 0)
            return;

        _research.ModifyServerPoints(server.Value, points, serverComp);

        var coordinates = Transform(crusher).Coordinates;
        foreach (var output in outputs)
        {
            _stack.Spawn(output.MaterialAmount, output.MaterialStack, coordinates);
            if (output.EmptyResult is { } emptyResult)
                Spawn(emptyResult, coordinates);

            Del(output.Input);
        }

        _popup.PopupEntity(
            Loc.GetString(
                "scientific-crusher-success",
                ("points", points),
                ("materials", materialUnits)),
            crusher,
            PopupType.Medium);

        if (_random.Prob(Math.Clamp(incidentChance, 0f, 0.95f)))
            DoIncident((crusher.Owner, crusher.Comp1));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query =
            EntityQueryEnumerator<ScientificCrusherComponent, EntityStorageComponent, ResearchClientComponent>();
        while (query.MoveNext(out var uid, out var crusher, out var storage, out var client))
        {
            if (!crusher.Crushing || crusher.CrushEndTime > _timing.CurTime)
                continue;

            FinishCrushing((uid, crusher, storage, client));
        }
    }

    private void DoIncident(Entity<ScientificCrusherComponent> crusher)
    {
        var coordinates = Transform(crusher).Coordinates;
        Spawn(crusher.Comp.IncidentEffect, coordinates);
        Spawn(crusher.Comp.IncidentRadiationEffect, coordinates);

        foreach (var (target, statusEffects) in _lookup.GetEntitiesInRange<StatusEffectsComponent>(
                     _transform.GetMapCoordinates(crusher),
                     crusher.Comp.IncidentShockRadius))
        {
            _electrocution.TryDoElectrocution(
                target,
                crusher,
                crusher.Comp.IncidentShockDamage,
                crusher.Comp.IncidentShockTime,
                true,
                statusEffects: statusEffects);
        }

        _damageable.TryChangeDamage(crusher, crusher.Comp.IncidentDamage);
        _popup.PopupEntity(Loc.GetString("scientific-crusher-incident"), crusher, PopupType.LargeCaution);
    }

    private static float GetIncidentChanceMultiplier(XenExperimentResultComponent? result)
    {
        return result?.Quality switch
        {
            XenExperimentResultQuality.Analyzed => 0.5f,
            XenExperimentResultQuality.Unstable => 2f,
            XenExperimentResultQuality.Spoiled => 1f,
            _ => 1f
        };
    }

    private sealed record CrusherOutput(
        EntityUid Input,
        ProtoId<Content.Shared.Stacks.StackPrototype> MaterialStack,
        int MaterialAmount,
        EntProtoId? EmptyResult);
}
