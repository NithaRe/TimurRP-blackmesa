using System.Linq;
using Content.Server.DoAfter;
using Content.Shared._BlackM.Evac;
using Content.Shared.DoAfter;
using Content.Shared.Mobs.Components;
using Content.Shared.Teleportation.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;

namespace Content.Server._BlackM.Evac;

public sealed class EvacPortalSystem : EntitySystem
{
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    private const float ProximityRange = 1.2f;
    private const float TeleportDelay = 3f;
    private const float UpdateInterval = 0.5f;

    private float _updateTimer = 0f;

    public override void Initialize()
    {
        SubscribeLocalEvent<EvacPortalComponent, EvacPortalDoAfterEvent>(OnDoAfterComplete);
    }

    public override void Update(float frameTime)
    {
        _updateTimer += frameTime;
        if (_updateTimer < UpdateInterval)
            return;
        _updateTimer = 0f;

        var portalQuery = EntityQueryEnumerator<EvacPortalComponent, TransformComponent>();
        while (portalQuery.MoveNext(out var portalUid, out var portal, out var portalXform))
        {
            if (!TryComp<LinkedEntityComponent>(portalUid, out var link) || link.LinkedEntities.Count == 0)
            {
                portal.ActiveDoAfters.Clear();
                continue;
            }

            var nearbyEntities = _lookup.GetEntitiesInRange(portalXform.Coordinates, ProximityRange);

            foreach (var entity in nearbyEntities)
            {
                if (!HasComp<MobStateComponent>(entity))
                    continue;

                if (portal.ActiveDoAfters.Contains(entity))
                    continue;

                portal.ActiveDoAfters.Add(entity);

                _doAfter.TryStartDoAfter(new DoAfterArgs(
                    EntityManager,
                    entity,
                    TeleportDelay,
                    new EvacPortalDoAfterEvent(),
                    portalUid,
                    target: portalUid)
                {
                    BreakOnMove = true,
                    BreakOnDamage = false,
                    NeedHand = false,
                });
            }

            var toRemove = portal.ActiveDoAfters
                .Where(e => !nearbyEntities.Contains(e))
                .ToList();

            foreach (var e in toRemove)
                portal.ActiveDoAfters.Remove(e);
        }
    }

    private void OnDoAfterComplete(EntityUid portalUid, EvacPortalComponent portal, EvacPortalDoAfterEvent args)
    {
        portal.ActiveDoAfters.Remove(args.User);

        if (args.Cancelled || args.Handled)
            return;

        if (!TryComp<LinkedEntityComponent>(portalUid, out var link) || link.LinkedEntities.Count == 0)
            return;

        var dest = link.LinkedEntities.First();
        if (!Exists(dest))
            return;

        _transform.SetCoordinates(args.User, Transform(dest).Coordinates);
        args.Handled = true;
    }
}