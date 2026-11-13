using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;

namespace Content.Server._BlackM.Events.Fog;

public sealed class ComplexFogRule : StationEventSystem<ComplexFogRuleComponent>
{
    [Dependency] private readonly ComplexFogSystem _fog = default!;

    protected override void Started(EntityUid uid, ComplexFogRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        component.AffectedEntities.Clear();

        if (!TryGetRandomStation(out var station) || !TryComp<StationDataComponent>(station.Value, out var data))
            return;

        foreach (var grid in data.Grids)
        {
            if (Deleted(grid))
                continue;

            if (component.AffectedEntities.Add(grid))
                _fog.StartFog(grid, component.FadeInTime, component.Density, component.Color);

            if (!component.IncludeMaps || Transform(grid).MapUid is not { } map)
                continue;

            if (component.AffectedEntities.Add(map))
                _fog.StartFog(map, component.FadeInTime, component.Density, component.Color);
        }
    }

    protected override void Ended(EntityUid uid, ComplexFogRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        foreach (var ent in component.AffectedEntities)
        {
            _fog.StopFog(ent, component.FadeOutTime);
        }

        component.AffectedEntities.Clear();
    }
}
