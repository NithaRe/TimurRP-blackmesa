using Content.Shared._BlackM.Elevator;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Server._BlackM.Underground;

public sealed record UndergroundCabin(EntityUid Map, MapId MapId, EntityUid Grid, EntityUid Controller, EntityUid Origin);

public sealed class UndergroundCabinSystem : EntitySystem
{
    [Dependency] private readonly MapLoaderSystem _loader = default!;
    private static readonly ResPath CabinPath = new("/Maps/_BlackM/Underground/elevator_cabin.yml");
    private const string CabinId = "underground-expedition";

    public bool TryLoadCabin(out UndergroundCabin? cabin)
    {
        cabin = null;
        var options = new DeserializationOptions { InitializeMaps = true, PauseMaps = false };
        if (!_loader.TryLoadMap(CabinPath, out var map, out _, options) || map == null)
            return false;

        var mapUid = map.Value.Owner;
        var mapId = map.Value.Comp.MapId;
        EntityUid? controller = null;
        EntityUid? grid = null;
        EntityUid? origin = null;
        var good = false;

        try
        {
            var controllers = EntityQueryEnumerator<BlackMElevatorComponent, TransformComponent>();
            while (controllers.MoveNext(out var uid, out var component, out var xform))
            {
                if (xform.MapID != mapId || component.Id != CabinId || component.Evacuation)
                    continue;
                if (controller != null || xform.GridUid == null)
                    return false;

                controller = uid;
                grid = xform.GridUid;
            }

            if (controller == null || grid == null)
                return false;

            var markers = EntityQueryEnumerator<BlackMElevatorSpawnPointComponent, TransformComponent>();
            while (markers.MoveNext(out var uid, out _, out var xform))
            {
                if (xform.MapID != mapId || xform.GridUid != grid)
                    continue;
                if (origin != null || xform.ParentUid != grid)
                    return false;
                origin = uid;
            }

            if (origin == null)
                return false;

            cabin = new UndergroundCabin(mapUid, mapId, grid.Value, controller.Value, origin.Value);
            good = true;
            return true;
        }
        finally
        {
            if (!good && !TerminatingOrDeleted(mapUid))
                QueueDel(mapUid);
        }
    }
}
