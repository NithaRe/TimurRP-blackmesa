using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Decals;
using Content.Shared.Decals;
using Robust.Server.Physics;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._BlackM.Map;

public sealed class DepartmentSpawnerSystem : EntitySystem
{
    [Dependency] private readonly MapLoaderSystem _loader = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly GridFixtureSystem _fixtures = default!;
    [Dependency] private readonly DecalSystem _decals = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DepartmentSpawnerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<DepartmentSpawnerComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Spawned)
            return;

        ent.Comp.Spawned = true;
        var xform = Transform(ent);
        if (xform.ParentUid != xform.GridUid || !TryComp(xform.GridUid, out MapGridComponent? target))
        {
            Log.Error($"Department marker {ToPrettyString(ent)} must be a direct child of the station grid.");
            return;
        }

        if (ent.Comp.Variants.Count == 0)
        {
            Log.Warning($"Department marker {ToPrettyString(ent)} has no variants configured.");
            return;
        }

        var path = _random.Pick(ent.Comp.Variants);
        var staging = _maps.CreateMap(out var stagingId, runMapInit: false);
        try
        {
            // MapInit must run on the final grid, after the entire department has been transferred.
            if (!_loader.TryLoadGrid(stagingId, path, out var source,
                    new DeserializationOptions { InitializeMaps = false }))
                return;

            if (!TryPlace(ent, (xform.GridUid.Value, target), source.Value, out var error))
            {
                Log.Error($"Cannot place department {path} at {ToPrettyString(ent)}: {error}");
                return;
            }

            Log.Info($"Placed department {path} at {xform.LocalPosition} on {ToPrettyString(target.Owner)}.");
            QueueDel(ent);
        }
        finally
        {
            Del(staging);
        }
    }

    private bool TryPlace(Entity<DepartmentSpawnerComponent> marker, Entity<MapGridComponent> target,
        Entity<MapGridComponent> source, out string error)
    {
        error = string.Empty;
        if (source.Comp.TileSize != 1 || target.Comp.TileSize != 1)
        {
            error = "Only grids with one-meter tiles are supported.";
            return false;
        }

        var children = Children(source);
        var origins = children.Where(HasComp<DepartmentOriginComponent>).ToArray();
        if (origins.Length != 1)
        {
            error = "The module must contain exactly one department origin marker on its grid.";
            return false;
        }

        var allEntities = new List<EntityUid>(children);
        for (var i = 0; i < allEntities.Count; i++)
        {
            var uid = allEntities[i];
            if (HasComp<DepartmentSpawnerComponent>(uid))
            {
                error = "Nested department spawners are not supported.";
                return false;
            }
            allEntities.AddRange(Children(uid));
        }

        var origin = Transform(origins[0]);
        var destination = Transform(marker);
        if (!IsTileCenter(origin.LocalPosition) || !IsTileCenter(destination.LocalPosition))
        {
            error = "Both markers must be at tile centers.";
            return false;
        }

        var offset = destination.LocalPosition.Floored() - origin.LocalPosition.Floored();
        var matrix = Matrix3x2.CreateTranslation(offset);
        var tiles = _maps.GetAllTiles(source, source.Comp).ToArray();
        if (tiles.Length == 0)
        {
            error = "The module has no tiles.";
            return false;
        }

        var indices = tiles.ToDictionary(t => t.GridIndices, t => t.GridIndices + offset);
        var footprint = indices.Values.ToHashSet();
        foreach (var child in children)
        {
            if (!indices.ContainsKey(Transform(child).LocalPosition.Floored()))
            {
                error = $"Module entity {ToPrettyString(child)} is outside its floor tiles.";
                return false;
            }
        }

        var existing = Children(target)
            .Where(uid => uid != marker.Owner && footprint.Contains(Transform(uid).LocalPosition.Floored()))
            .ToArray();
        if (existing.Any(HasComp<DepartmentSpawnerComponent>))
        {
            error = "The module overlaps another department marker.";
            return false;
        }

        // Replace only occupied module tiles, never the bounding rectangle or its holes.
        foreach (var uid in existing)
            Del(uid);

        if (TryComp(target, out DecalGridComponent? oldDecals))
        {
            foreach (var chunk in oldDecals.ChunkCollection.ChunkCollection.Values.ToArray())
            foreach (var (id, decal) in chunk.Decals.ToArray())
            {
                if (footprint.Contains(decal.Coordinates.Floored()))
                    _decals.RemoveDecal(target, id);
            }
        }

        var decals = TryComp(source, out DecalGridComponent? sourceDecals)
            ? sourceDecals.ChunkCollection.ChunkCollection.Values.SelectMany(c => c.Decals.Values).ToArray()
            : Array.Empty<Decal>();
        _atmos.CopyDepartmentAtmosphere(source, target, indices);
        Del(origins[0]);

        // Engine reanchoring requires both grids to be on the same map.
        _transform.SetCoordinates(source, new EntityCoordinates(Transform(target).MapUid!.Value,
            _transform.GetWorldPosition(target)));

        // Overwrite tiles in place so the station never splits during replacement.
        _maps.SetTiles(target, target.Comp, tiles.Select(t => (indices[t.GridIndices], t.Tile)).ToList());
        // Move unanchored children explicitly: marker entities need not have lookup fixtures.
        foreach (var child in children)
        {
            if (Deleted(child))
                continue;
            var xform = Transform(child);
            if (!xform.Anchored)
            {
                _transform.SetCoordinates(child, xform,
                    new EntityCoordinates(target, xform.LocalPosition + offset),
                    xform.LocalRotation);
            }
        }

        _fixtures.Merge(target, source, matrix);
        foreach (var decal in decals)
        {
            var coords = new EntityCoordinates(target, Vector2.Transform(decal.Coordinates, matrix));
            _decals.TryAddDecal(decal.Id, coords, out _, decal.Color, decal.Angle,
                decal.ZIndex, decal.Cleanable);
        }

        // The outer map initialization has already enumerated the station grid's children.
        foreach (var uid in allEntities)
        {
            if (!Deleted(uid) && MetaData(uid).EntityLifeStage < EntityLifeStage.MapInitialized)
                EntityManager.RunMapInit(uid, MetaData(uid));
        }

        return true;
    }

    private static bool IsTileCenter(Vector2 position)
        => Vector2.DistanceSquared(position, (Vector2) position.Floored() + new Vector2(0.5f)) < 0.0001f;

    private List<EntityUid> Children(EntityUid uid)
    {
        var result = new List<EntityUid>();
        var enumerator = Transform(uid).ChildEnumerator;
        while (enumerator.MoveNext(out var child))
            result.Add(child);
        return result;
    }
}
