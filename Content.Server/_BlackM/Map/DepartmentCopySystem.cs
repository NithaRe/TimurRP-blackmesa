using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Content.Server.Administration.Managers;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Decals;
using Content.Server.Mapping;
using Content.Shared._BlackM.Map;
using Content.Shared.Administration;
using Content.Shared.Decals;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Content.Server._BlackM.Map;

public sealed class DepartmentCopySystem : EntitySystem
{
    [Dependency] private readonly IAdminManager _admins = default!;
    [Dependency] private readonly MapLoaderSystem _loader = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly DecalSystem _decals = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly MappingSystem _mapping = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private TimeSpan _nextCopy;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<DepartmentCopyRequest>(OnCopy);
    }

    private void OnCopy(DepartmentCopyRequest msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!_admins.HasAdminFlag(session, AdminFlags.Mapping))
            return;

        if (_timing.RealTime < _nextCopy)
        {
            Reply(session, Loc.GetString("department-copy-busy"));
            return;
        }

        _nextCopy = _timing.RealTime + TimeSpan.FromSeconds(2);
        var source = GetEntity(msg.Grid);
        if (session.AttachedEntity is not { } player ||
            !TryComp(source, out TransformComponent? sourceTransform) ||
            Transform(player).MapUid != sourceTransform.MapUid)
        {
            Reply(session, Loc.GetString("department-copy-invalid"));
            return;
        }

        try
        {
            if (!TryCopy(source, msg.Tiles, out var copy, out var error))
            {
                Reply(session, Loc.GetString(error));
                return;
            }

            var origin = Children(copy).Single(HasComp<DepartmentOriginComponent>);
            _transform.SetCoordinates(player, new EntityCoordinates(copy, Transform(origin).LocalPosition));
            _mapping.ToggleAutosave(copy, "department-copy");
            Reply(session, Loc.GetString("department-copy-success",
                ("map", Transform(copy).MapID.ToString()), ("grid", GetNetEntity(copy).ToString())), true);
        }
        catch (Exception e)
        {
            Log.Error($"Department copy failed: {e}");
            Reply(session, Loc.GetString("department-copy-failed"));
        }
    }

    private void Reply(ICommonSession session, string message, bool success = false)
        => RaiseNetworkEvent(new DepartmentCopyResult(message, success), session.Channel);

    public bool TryCopy(EntityUid source, IReadOnlyCollection<Vector2i> selection, out EntityUid copy, out string error)
    {
        copy = default;
        error = "department-copy-invalid";
        if (selection.Count == 0 || selection.Count > DepartmentCopyRequest.MaxTiles ||
            !TryComp(source, out MapGridComponent? sourceGrid) || sourceGrid.TileSize != 1)
            return false;

        // Copy design data only. Do not duplicate a running station or its players.
        if (MetaData(source).EntityLifeStage >= EntityLifeStage.MapInitialized)
        {
            error = "department-copy-mapping-only";
            return false;
        }

        var tiles = selection.ToHashSet();
        tiles.RemoveWhere(index => _maps.GetTileRef(source, sourceGrid, index).Tile.IsEmpty);
        if (tiles.Count == 0)
            return false;

        void Filter(Entity<MetaDataComponent> ent, ref bool serializable)
            => serializable &= !HasComp<ActorComponent>(ent) &&
                               !HasComp<DepartmentSpawnerComponent>(ent) &&
                               !HasComp<DepartmentOriginComponent>(ent);

        // Clone before cropping so links crossing the selection boundary have distinct targets.
        // Serializing them all as Invalid would collide in entity-keyed dictionaries.
        // Only the private, uninitialized copy is cropped; the source is never edited.
        string yaml;
        _loader.OnIsSerializable += Filter;
        try
        {
            var (node, _) = _loader.SerializeEntitiesRecursive([source], new SerializationOptions
            {
                Category = FileCategory.Grid,
                ExpectPreInit = true,
                MissingEntityBehaviour = MissingEntityBehaviour.Ignore,
            });
            using var writer = new StringWriter();
            new YamlStream(new YamlDocument(node.ToYaml())).Save(new YamlMappingFix(new Emitter(writer)), false);
            yaml = writer.ToString();
        }
        finally
        {
            _loader.OnIsSerializable -= Filter;
        }

        var map = _maps.CreateMap(out var mapId, runMapInit: false);
        var success = false;
        LoadResult? loaded = null;
        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(yaml));
            if (!_loader.TryLoadGeneric(stream, "department-copy", out loaded, new MapLoadOptions
                {
                    MergeMap = mapId,
                    ExpectedCategory = FileCategory.Grid,
                    DeserializationOptions = new DeserializationOptions { InitializeMaps = false, LogInvalidEntities = false },
                }) || loaded.Grids.Count != 1)
            {
                error = "department-copy-failed";
                return false;
            }

            var grid = loaded.Grids.Single();
            // An irregular selection may have disconnected islands; keep it as one editable grid.
            grid.Comp.CanSplit = false;
            _transform.SetCoordinates(grid.Owner, new EntityCoordinates(map, Vector2.Zero));
            _transform.SetLocalRotation(grid.Owner, Angle.Zero);
            foreach (var child in Children(grid))
            {
                if (!tiles.Contains(Transform(child).LocalPosition.Floored()))
                    Del(child);
            }
            _maps.SetTiles(grid, grid.Comp, _maps.GetAllTiles(grid, grid.Comp)
                .Where(tile => !tiles.Contains(tile.GridIndices))
                .Select(tile => (tile.GridIndices, Tile.Empty)).ToList());

            if (TryComp(grid, out DecalGridComponent? decals))
            {
                foreach (var chunk in decals.ChunkCollection.ChunkCollection.Values.ToArray())
                foreach (var (id, decal) in chunk.Decals.ToArray())
                {
                    if (!tiles.Contains(decal.Coordinates.Floored()))
                        _decals.RemoveDecal(grid, id);
                }
            }

            _atmos.TrimDepartmentAtmosphere(grid, tiles);

            var sourceMarkers = Children(source).Where(uid => tiles.Contains(Transform(uid).LocalPosition.Floored())).ToArray();
            var origins = sourceMarkers.Where(HasComp<DepartmentOriginComponent>).ToArray();
            if (origins.Length == 0)
                origins = sourceMarkers.Where(HasComp<DepartmentSpawnerComponent>).ToArray();
            var origin = origins.Length == 1
                ? Transform(origins[0]).LocalPosition.Floored()
                : tiles.OrderBy(i => i.X).ThenBy(i => i.Y).First();
            Spawn("BlackMDepartmentOrigin", new EntityCoordinates(grid, (Vector2) origin + new Vector2(0.5f)));
            copy = grid;
            success = true;
            error = string.Empty;
            return true;
        }
        finally
        {
            if (!success)
            {
                if (loaded != null)
                    _loader.Delete(loaded);
                if (!Deleted(map))
                    Del(map);
            }
        }
    }

    private List<EntityUid> Children(EntityUid uid)
    {
        var children = new List<EntityUid>();
        var enumerator = Transform(uid).ChildEnumerator;
        while (enumerator.MoveNext(out var child))
            children.Add(child);
        return children;
    }
}
