using Content.Shared.Doors;
using Content.Shared.Physics;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;

namespace Content.Server._BlackM.Doors;

public sealed class BlackMWideDoorSafetySystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    private readonly HashSet<Entity<PhysicsComponent>> _intersecting = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BlackMWideDoorSafetyComponent, BeforeDoorClosedEvent>(OnBeforeClose);
    }

    private void OnBeforeClose(EntityUid uid, BlackMWideDoorSafetyComponent component, BeforeDoorClosedEvent args)
    {
        if (args.Cancelled || !args.PerformCollisionCheck || !TryComp<PhysicsComponent>(uid, out var physics))
            return;

        var xform = Transform(uid);
        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return;

        var tile = _map.GetTileRef(xform.GridUid.Value, grid, xform.Coordinates);
        for (var offset = -2; offset <= 2; offset++)
        {
            _intersecting.Clear();
            _lookup.GetLocalEntitiesIntersecting(xform.GridUid.Value, tile.GridIndices + new Vector2i(offset, 0),
                _intersecting, gridComp: grid, flags: LookupFlags.All & ~LookupFlags.Sensors);

            foreach (var other in _intersecting)
            {
                var body = other.Comp;
                if (body == physics || !body.CanCollide)
                    continue;
                if (body.CollisionLayer == (int) CollisionGroup.GlassLayer ||
                    body.CollisionLayer == (int) CollisionGroup.GlassAirlockLayer ||
                    body.CollisionLayer == (int) CollisionGroup.TableLayer ||
                    body.CollisionLayer == (int) CollisionGroup.ConveyorMask)
                    continue;
                if ((body.CollisionMask & (int) CollisionGroup.LowImpassable) == 0)
                    continue;
                if ((physics.CollisionMask & body.CollisionLayer) == 0 &&
                    (body.CollisionMask & physics.CollisionLayer) == 0)
                    continue;

                args.Cancel();
                return;
            }
        }
    }
}
