using System.Linq;
using System.IO;
using System.Text;
using System.Numerics;
using Content.Client.Humanoid;
using Content.Client.Clothing;
using Content.Client.Lobby;
using Content.Client.Viewport;
using Content.Shared._BlackM.WalkBob;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.Physics;
using Content.Shared.Preferences;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using YamlDotNet.RepresentationModel;

namespace Content.Client._BlackM.Rules;

/// <summary>Owns an isolated, paused client copy of the entrance and two presentation-only actors.</summary>
public sealed class RulesArrivalScene : Control
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IComponentFactory _components = default!;
    [Dependency] private readonly IClientPreferencesManager _preferences = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    private readonly ScalingViewport _viewport;
    private readonly Robust.Shared.Graphics.Eye _eye = new() { DrawFov = false, DrawLight = true, Zoom = new Vector2(1.0f) };
    private static readonly Vector2 Entrance = new(9.5f, -0.5f);
    private static readonly Vector2 Checkpoint = new(19.1f, -0.5f);
    private static readonly Vector2 DeskPosition = new(18.5f, 0.5f);
    private static readonly PhysShapeCircle WalkingShape = new(0.3f);
    private float _roadSouth;
    private float _roadNorth;
    private EntityUid? _map;
    private EntityUid _grid;
    private EntityUid _employee;
    private EntityUid _guard;
    private EntityUid? _documents;
    private readonly List<EntityUid> _shutters = new();
    private readonly HashSet<EntityUid> _movementObstacles = new();
    private bool _opened;
    private Vector2 _position = Entrance;
    public bool ProfileReady => _preferences.Preferences?.SelectedCharacter is HumanoidCharacterProfile;
    public bool NearCheckpoint => Vector2.DistanceSquared(_position, DeskPosition) <= 5f;
    public bool InElevator => _opened && _position.X >= 23.5f;

    public RulesArrivalScene()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Ignore;
        _viewport = new ScalingViewport
        {
            ViewportSize = new Vector2i(640, 360),
            Visible = false,
            AlwaysRender = false,
            MouseFilter = MouseFilterMode.Ignore,
            StretchMode = ScalingViewportStretchMode.Nearest,
            RenderScaleMode = ScalingViewportRenderScaleMode.CeilInt,
        };
        AddChild(_viewport);
    }

    public bool TryStart()
    {
        if (_map != null)
            return true;

        var loader = _entities.System<MapLoaderSystem>();
        if (!loader.TryReadFile(new ResPath("/Maps/_BlackM/evacmap.yml"), out var data))
            return false;

        // The scene needs geometry and visuals, not server-only map components or saved device links.
        foreach (var group in (SequenceDataNode) data["entities"])
        foreach (var entity in (SequenceDataNode) ((MappingDataNode) group)["entities"])
        {
            if (!((MappingDataNode) entity).TryGet<SequenceDataNode>("components", out var components))
                continue;

            for (var i = components.Count - 1; i >= 0; i--)
            {
                var type = ((MappingDataNode) components[i]).Get<ValueDataNode>("type").Value;
                if (!_components.TryGetRegistration(type, out _) || type is "DeviceLinkSource" or "GasTileOverlay" or "MapAtmosphere")
                    components.RemoveAt(i);
            }
        }

        // StringWriter is not allowed by the sandbox, so write straight into a MemoryStream.
        using var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: true))
        {
            new YamlStream(new YamlDocument(data.ToYaml())).Save(writer, false);
        }
        stream.Position = 0;

        if (!loader.TryLoadGeneric(stream, "rules-arrival", out var loaded, new MapLoadOptions
            { DeserializationOptions = new DeserializationOptions { PauseMaps = true }, ExpectedCategory = FileCategory.Map }))
            return false;

        if (loaded.Maps.Count != 1 || loaded.Grids.Count != 1)
        {
            foreach (var entity in loaded.Entities)
                if (_entities.EntityExists(entity))
                    _entities.QueueDeleteEntity(entity);
            return false;
        }

        var map = loaded.Maps.First();
        _map = map.Owner;
        _grid = loaded.Grids.First().Owner;
        SetRoadEdges();

        foreach (var entity in loaded.Entities)
            if (_entities.GetComponent<MetaDataComponent>(entity).EntityPrototype?.ID == "ShuttersNormal")
                _shutters.Add(entity);

        var profile = _preferences.Preferences?.SelectedCharacter as HumanoidCharacterProfile
            ?? HumanoidCharacterProfile.RandomWithSpecies();
        _employee = SpawnActor(Entrance, "ClothingUniformJumpsuitFlannel", "ClothingShoesColorWhite", profile);
        _guard = SpawnActor(new Vector2(20.2f, -0.5f), "ClothingUniformJumpsuitKovakSoldier", "ClothingShoesKovakSoldier");
        Equip(_guard, "ClothingOuterArmorKovakSoldier", "outerClothing");
        Equip(_guard, "ClothingHeadHelmetKovakSoldier", "head");
        _entities.System<ClientClothingSystem>().InitClothing(_guard, _entities.GetComponent<InventoryComponent>(_guard));
        var desk = _entities.SpawnEntity("Table", new EntityCoordinates(_grid, DeskPosition));
        // Use the table's snapped position, not its requested spawn coordinates.
        var deskCoordinates = _entities.GetComponent<TransformComponent>(desk).Coordinates;
        _documents = _entities.SpawnEntity("BoxFolderBlue", deskCoordinates);
        var sprites = _entities.System<SpriteSystem>();
        var folderSprite = _entities.GetComponent<SpriteComponent>(_documents.Value);
        sprites.SetOffset((_documents.Value, folderSprite), new Vector2(0, 0.125f));
        sprites.SetDrawDepth((_documents.Value, folderSprite), _entities.GetComponent<SpriteComponent>(desk).DrawDepth + 1);
        var transform = _entities.System<SharedTransformSystem>();
        transform.SetLocalRotation(_guard, Direction.West.ToAngle());
        _eye.Position = transform.ToMapCoordinates(new EntityCoordinates(_grid, new Vector2(17.5f, -0.5f)));
        _viewport.Eye = _eye;
        _viewport.Visible = true;
        _viewport.AlwaysRender = true;
        SetProgress(0, 0);
        return true;
    }

    private void SetRoadEdges()
    {
        var map = _entities.System<SharedMapSystem>();
        var grid = _entities.GetComponent<MapGridComponent>(_grid);
        var column = (int) MathF.Floor(Entrance.X / grid.TileSize);
        var row = (int) MathF.Floor(Entrance.Y / grid.TileSize);
        var road = map.GetTileRef(_grid, grid, new Vector2i(column, row)).Tile.TypeId;
        var south = row;
        var north = row;
        // Read the asphalt strip at the entrance, away from the checkpoint furniture.
        for (var i = 0; i < 16; i++)
        {
            if (map.GetTileRef(_grid, grid, new Vector2i(column, south - 1)).Tile.TypeId != road)
                break;
            south--;
        }
        for (var i = 0; i < 16; i++)
        {
            if (map.GetTileRef(_grid, grid, new Vector2i(column, north + 1)).Tile.TypeId != road)
                break;
            north++;
        }
        _roadSouth = south * grid.TileSize;
        _roadNorth = (north + 1) * grid.TileSize;
    }

    private EntityUid SpawnActor(Vector2 position, string uniform, string shoes, HumanoidCharacterProfile? profile = null)
    {
        profile ??= HumanoidCharacterProfile.DefaultWithSpecies();
        var doll = _prototypes.Index<SpeciesPrototype>(profile.Species).DollPrototype;
        var actor = _entities.SpawnEntity(doll, new EntityCoordinates(_grid, position));
        _entities.System<HumanoidAppearanceSystem>().LoadProfile(actor, profile);
        foreach (var (prototype, slot) in new[] { (uniform, "jumpsuit"), (shoes, "shoes") })
        {
            Equip(actor, prototype, slot);
        }
        _entities.System<SharedTransformSystem>().SetLocalRotation(actor, Direction.East.ToAngle());
        // Only presentation actors animate; the copied map stays paused.
        _entities.System<MetaDataSystem>().SetEntityPaused(actor, false);
        _entities.EnsureComponent<WalkBobComponent>(actor).VisualSpeedOverride = 0f;
        return actor;
    }

    private void Equip(EntityUid actor, string prototype, string slot)
    {
        var item = _entities.SpawnEntity(prototype, new EntityCoordinates(actor, Vector2.Zero));
        if (!_entities.System<InventorySystem>().TryEquip(actor, item, slot, silent: true, force: true))
            _entities.QueueDeleteEntity(item);
    }

    public EntityUid? Employee => _map != null && _entities.EntityExists(_employee) ? _employee : null;

    public bool TryGetSpeakerPosition(out Vector2 position)
    {
        position = default;
        if (_map == null || !_entities.TryGetComponent<TransformComponent>(_guard, out var transform))
            return false;
        var coordinates = _entities.System<SharedTransformSystem>().ToMapCoordinates(transform.Coordinates);
        position = (_viewport.WorldToScreen(coordinates.Position) - GlobalPixelPosition) / UIScale;
        return true;
    }

    public void SetProgress(float arrival, float departure)
    {
        if (_map == null || !_entities.EntityExists(_employee))
            return;
        var transform = _entities.System<SharedTransformSystem>();
        var position = Vector2.Lerp(Entrance, Checkpoint, Math.Clamp(arrival, 0, 1));
        transform.SetLocalRotation(_employee, Direction.East.ToAngle());
        _position = position;
        transform.SetLocalPositionNoLerp(_employee, position);
    }

    public void MoveEmployee(Vector2 input, float frameTime)
    {
        if (_map == null)
            return;
        if (input == Vector2.Zero)
        {
            SetWalkingSpeed(_employee, 0f);
            return;
        }
        var previous = _position;
        var direction = Vector2.Normalize(input);
        var delta = direction * (3f * Math.Min(frameTime, 0.1f));
        TryMove(new Vector2(delta.X, 0));
        TryMove(new Vector2(0, delta.Y));
        var transform = _entities.System<SharedTransformSystem>();
        transform.SetLocalRotation(_employee, direction.ToWorldAngle());
        transform.SetLocalPositionNoLerp(_employee, _position);
        SetWalkingSpeed(_employee, frameTime > 0 ? Vector2.Distance(previous, _position) / frameTime : 0f);
    }

    private void TryMove(Vector2 delta)
    {
        if (delta == Vector2.Zero)
            return;
        // Short steps prevent crossing thin fixtures; X/Y are resolved separately for wall sliding.
        var steps = Math.Max(1, (int) MathF.Ceiling(delta.Length() / 0.05f));
        var step = delta / steps;
        for (var i = 0; i < steps; i++)
        {
            var target = _position + step;
            if (!_opened && target.X > 19.4f)
                return;
            if (IsBlocked(target))
                return;
            _position = target;
        }
    }

    private bool IsBlocked(Vector2 position)
    {
        // Invisible road-edge barriers use the same body radius on both sides.
        if (position.Y - WalkingShape.Radius < _roadSouth || position.Y + WalkingShape.Radius > _roadNorth)
            return true;
        var coordinates = _entities.System<SharedTransformSystem>()
            .ToMapCoordinates(new EntityCoordinates(_grid, position));
        var bodyTransform = new Robust.Shared.Physics.Transform(coordinates.Position, Angle.Zero);
        var bounds = WalkingShape.ComputeAABB(bodyTransform, 0);
        var physics = _entities.System<SharedPhysicsSystem>();
        _movementObstacles.Clear();
        // Include paused/non-simulating bodies in the local map, using their real fixtures.
        _entities.System<EntityLookupSystem>().GetEntitiesIntersecting(
            coordinates.MapId, WalkingShape, bodyTransform, _movementObstacles,
            LookupFlags.Static | LookupFlags.Dynamic | LookupFlags.Sundries);

        foreach (var uid in _movementObstacles)
        {
            if (uid == _employee || (_opened && _shutters.Contains(uid)) ||
                !_entities.TryGetComponent<FixturesComponent>(uid, out var fixtures))
                continue;

            var obstacleTransform = physics.GetPhysicsTransform(uid);

            foreach (var fixture in fixtures.Fixtures.Values)
            {
                if (!fixture.Hard || (fixture.CollisionLayer & (int) CollisionGroup.MobMask) == 0)
                    continue;

                for (var child = 0; child < fixture.Shape.ChildCount; child++)
                {
                    var obstacleBounds = fixture.Shape.ComputeAABB(obstacleTransform, child);
                    if (obstacleBounds.Intersects(bounds))
                        return true;
                }
            }
        }

        return false;
    }

    public bool IsCheckpointClick(Vector2 screenPosition)
    {
        if (_map == null || _documents is not { } documents)
            return false;
        var world = _viewport.ScreenToMap(screenPosition);
        var transform = _entities.System<SharedTransformSystem>();
        var local = transform.ToCoordinates(documents, world).Position;
        var offset = _entities.GetComponent<SpriteComponent>(documents).Offset;
        // Match the visible folder, not the full tile or the inspector beside it.
        return Math.Abs(local.X - offset.X) <= 0.28f && Math.Abs(local.Y - offset.Y) <= 0.22f;
    }

    public void OpenPassage()
    {
        _opened = true;
        if (_documents is { } documents)
            _entities.QueueDeleteEntity(documents);
        _documents = null;
        foreach (var shutter in _shutters)
            if (_entities.TryGetComponent<SpriteComponent>(shutter, out var sprite))
                _entities.System<SpriteSystem>().LayerSetRsiState((shutter, sprite), 0, "open");
    }

    public void StepGuardAside(float progress)
    {
        var step = Math.Clamp(progress, 0, 1);
        var transform = _entities.System<SharedTransformSystem>();
        transform.SetLocalRotation(_guard, step < 1 ? Direction.South.ToAngle() : Direction.West.ToAngle());
        transform.SetLocalPositionNoLerp(_guard, new Vector2(20.2f, -0.5f - step));
        SetWalkingSpeed(_guard, step > 0 && step < 1 ? 1.25f : 0f);
    }

    private void SetWalkingSpeed(EntityUid actor, float speed)
    {
        _entities.GetComponent<WalkBobComponent>(actor).VisualSpeedOverride = speed;
    }

    public void Release()
    {
        _viewport.Visible = false;
        _viewport.AlwaysRender = false;
        _viewport.Eye = null;
        var map = _map;
        _map = null;
        // Defer deletion until render and physics traversal have finished.
        if (map is { } uid && _entities.EntityExists(uid))
            _entities.QueueDeleteEntity(uid);
        _shutters.Clear();
        _movementObstacles.Clear();
        _opened = false;
        _documents = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Release();
        base.Dispose(disposing);
    }
}
