using System.Numerics;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.Server._BlackM.Underground;

public enum UndergroundCargoMoveResult : byte
{
    Moved,
    Rejected,
    RolledBack,
    PartialFailure,
}

public sealed class UndergroundCargoSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transforms = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;

    public bool TryCollect(EntityUid origin, float halfWidth, out List<EntityUid> cargo)
    {
        cargo = new List<EntityUid>();
        if (TerminatingOrDeleted(origin) || halfWidth <= 0f || halfWidth > 1.5f)
            return false;

        var marker = Transform(origin);
        if (marker.GridUid is not { } grid || marker.ParentUid != grid)
            return false;

        var children = Transform(grid).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (child == origin || TerminatingOrDeleted(child))
                continue;

            var xform = Transform(child);
            if (xform.ParentUid != grid || xform.Anchored)
                continue;

            var offset = xform.LocalPosition - marker.LocalPosition;
            if (MathF.Abs(offset.X) >= halfWidth || MathF.Abs(offset.Y) >= halfWidth)
                continue;

            if (TryComp<PhysicsComponent>(child, out var physics))
            {
                if (physics.BodyType == BodyType.Static)
                    continue;
            }
            else if (!HasComp<ItemComponent>(child) && !HasComp<MobStateComponent>(child))
            {
                continue;
            }

            cargo.Add(child);
        }

        return true;
    }

    public UndergroundCargoMoveResult MoveSnapshot(EntityUid origin, EntityUid destination,
        IReadOnlyCollection<EntityUid> cargo, float halfWidth)
    {
        if (TerminatingOrDeleted(origin) || TerminatingOrDeleted(destination) ||
            halfWidth <= 0f || halfWidth > 1.5f)
        {
            return UndergroundCargoMoveResult.Rejected;
        }

        var from = Transform(origin);
        var to = Transform(destination);
        if (from.GridUid is not { } sourceGrid || from.ParentUid != sourceGrid ||
            to.GridUid is not { } targetGrid || to.ParentUid != targetGrid)
        {
            return UndergroundCargoMoveResult.Rejected;
        }

        var snapshots = new Dictionary<EntityUid, Vector2>();
        foreach (var entity in cargo)
        {
            if (TerminatingOrDeleted(entity) || entity == origin || entity == destination || snapshots.ContainsKey(entity))
                return UndergroundCargoMoveResult.Rejected;

            var xform = Transform(entity);
            var offset = xform.LocalPosition - from.LocalPosition;
            if (xform.ParentUid != sourceGrid || xform.Anchored ||
                MathF.Abs(offset.X) >= halfWidth || MathF.Abs(offset.Y) >= halfWidth ||
                (TryComp<PhysicsComponent>(entity, out var body) && body.BodyType == BodyType.Static))
            {
                return UndergroundCargoMoveResult.Rejected;
            }

            snapshots.Add(entity, xform.LocalPosition);
        }

        var moved = new List<EntityUid>();
        try
        {
            foreach (var (entity, original) in snapshots)
            {
                _pulling.StopAllPulls(entity);
                if (TryComp<BuckleComponent>(entity, out var buckle) && buckle.Buckled)
                    _buckle.Unbuckle((entity, buckle), null);

                // Include the current entity in rollback if SetCoordinates fails after moving it.
                moved.Add(entity);
                var offset = original - from.LocalPosition;
                _transforms.SetCoordinates(entity, new EntityCoordinates(targetGrid, to.LocalPosition + offset));
            }

            return UndergroundCargoMoveResult.Moved;
        }
        catch (Exception e)
        {
            Log.Error($"Underground cargo transfer failed: {e}");
            var rolledBack = true;
            for (var index = moved.Count - 1; index >= 0; index--)
            {
                var entity = moved[index];
                if (TerminatingOrDeleted(entity) || TerminatingOrDeleted(sourceGrid))
                {
                    rolledBack = false;
                    continue;
                }

                try
                {
                    _transforms.SetCoordinates(entity, new EntityCoordinates(sourceGrid, snapshots[entity]));
                }
                catch (Exception rollbackError)
                {
                    Log.Error($"Underground cargo rollback failed for {ToPrettyString(entity)}: {rollbackError}");
                    rolledBack = false;
                }
            }

            return rolledBack ? UndergroundCargoMoveResult.RolledBack : UndergroundCargoMoveResult.PartialFailure;
        }
    }
}
