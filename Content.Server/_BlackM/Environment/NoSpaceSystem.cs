using Content.Server.Atmos.EntitySystems;
using Content.Server.Station.Events;
using Content.Shared._BlackM.Environment;
using Content.Shared.Atmos;
using Content.Shared.Gravity;
using Content.Shared.Light.Components;
using Content.Shared.Parallax;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.Environment;

public sealed class NoSpaceSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly TimeSpan RefillDelay = TimeSpan.FromSeconds(3);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NoSpaceComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NoSpaceComponent, StationPostInitEvent>(OnStationPostInit);
    }

    private void OnMapInit(Entity<NoSpaceComponent> ent, ref MapInitEvent args)
    {
        if (HasComp<MapComponent>(ent))
            Apply(ent);
    }

    private void OnStationPostInit(Entity<NoSpaceComponent> ent, ref StationPostInitEvent args)
    {
        var maps = new HashSet<EntityUid>();
        foreach (var grid in args.Station.Comp.Grids)
        {
            if (Transform(grid).MapUid is { } map)
                maps.Add(map);
        }

        foreach (var map in maps)
        {
            if (!TryComp<NoSpaceComponent>(map, out var comp))
            {
                comp = EntityManager.ComponentFactory.GetComponent<NoSpaceComponent>();
                CopySettings(ent.Comp, comp);
                AddComp(map, comp, true);
            }

            Apply((map, comp));
        }
    }

    private static void CopySettings(NoSpaceComponent from, NoSpaceComponent to)
    {
        to.Gravity = from.Gravity;
        to.Breathable = from.Breathable;
        to.Oxygen = from.Oxygen;
        to.Nitrogen = from.Nitrogen;
        to.Temperature = from.Temperature;
        to.FillVacuum = from.FillVacuum;
        to.FillBelowPressure = from.FillBelowPressure;
        to.SilenceWind = from.SilenceWind;
        to.NoSpaceAmbience = from.NoSpaceAmbience;
        to.MapLight = from.MapLight;
        to.Parallax = from.Parallax;
    }

    public void Apply(Entity<NoSpaceComponent> ent)
    {
        var (map, comp) = ent;
        if (comp.Applied || TerminatingOrDeleted(map) || !HasComp<MapComponent>(map))
            return;

        comp.Applied = true;
        Dirty(map, comp);

        if (comp.Gravity)
        {
            var gravity = EnsureComp<GravityComponent>(map);
            gravity.Inherent = true;
            if (!gravity.Enabled)
            {
                gravity.Enabled = true;
                var ev = new GravityChangedEvent(map, true);
                RaiseLocalEvent(map, ref ev, true);
            }

            Dirty(map, gravity);
        }

        if (comp.Breathable)
        {
            _atmos.SetMapAtmosphere(map, false, BuildMixture(comp));

            if (comp.FillVacuum)
            {
                FillVacuum(map, comp);
                comp.FillPassesLeft = 2;
                comp.NextFill = _timing.CurTime + RefillDelay;
            }
        }

        if (comp.MapLight is { } color)
        {
            var light = EnsureComp<MapLightComponent>(map);
            light.AmbientLightColor = color;
            Dirty(map, light);
        }

        if (!string.IsNullOrEmpty(comp.Parallax))
        {
            var parallax = EnsureComp<ParallaxComponent>(map);
            parallax.Parallax = comp.Parallax;
            Dirty(map, parallax);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<NoSpaceComponent, MapComponent>();
        while (query.MoveNext(out var uid, out var comp, out _))
        {
            if (comp.FillPassesLeft <= 0 || now < comp.NextFill)
                continue;

            comp.FillPassesLeft--;
            comp.NextFill = now + RefillDelay;
            FillVacuum(uid, comp);
        }
    }

    private static GasMixture BuildMixture(NoSpaceComponent comp)
    {
        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        moles[(int) Gas.Oxygen] = comp.Oxygen;
        moles[(int) Gas.Nitrogen] = comp.Nitrogen;
        return new GasMixture(moles, comp.Temperature);
    }

    private void FillVacuum(EntityUid map, NoSpaceComponent comp)
    {
        var mixture = BuildMixture(comp);
        var grids = EntityQueryEnumerator<MapGridComponent, TransformComponent>();
        while (grids.MoveNext(out var grid, out _, out var xform))
        {
            if (xform.MapUid != map)
                continue;

            foreach (var air in _atmos.GetAllMixtures(grid, true))
            {
                if (air.Immutable || air.Pressure >= comp.FillBelowPressure)
                    continue;

                air.CopyFrom(mixture);
            }
        }
    }
}
