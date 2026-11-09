using Content.Shared._BlackM.Turret;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.Server._BlackM.Turret;

public sealed class HecuTurretFoldSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    private static readonly SpriteSpecifier FoldIcon =
        new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/fold.svg.192dpi.png"));

    private const CollisionGroup BlockMask = CollisionGroup.MobMask | CollisionGroup.MobLayer;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HecuTurretComponent, GetVerbsEvent<AlternativeVerb>>(OnTurretVerbs);
        SubscribeLocalEvent<HecuTurretComponent, HecuTurretFoldDoAfterEvent>(OnFoldDoAfter);

        SubscribeLocalEvent<HecuTurretFoldedComponent, AfterInteractEvent>(OnFoldedAfterInteract);
        SubscribeLocalEvent<HecuTurretFoldedComponent, GetVerbsEvent<AlternativeVerb>>(OnFoldedVerbs);
        SubscribeLocalEvent<HecuTurretFoldedComponent, HecuTurretDeployDoAfterEvent>(OnDeployDoAfter);
    }

    #region Fold

    private void OnTurretVerbs(EntityUid uid, HecuTurretComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || args.Hands == null || comp.FoldedPrototype == null)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("hecu-turret-verb-fold"),
            Icon = FoldIcon,
            Act = () => StartFold(uid, comp, user),
            Priority = 2,
        });
    }

    private void StartFold(EntityUid uid, HecuTurretComponent comp, EntityUid user)
    {
        var doAfter = new DoAfterArgs(EntityManager, user, comp.FoldTime, new HecuTurretFoldDoAfterEvent(), uid, target: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("hecu-turret-fold-start"), uid, user);
    }

    private void OnFoldDoAfter(EntityUid uid, HecuTurretComponent comp, HecuTurretFoldDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || comp.FoldedPrototype == null || TerminatingOrDeleted(uid))
            return;

        args.Handled = true;

        var folded = SpawnAtPosition(comp.FoldedPrototype.Value, Transform(uid).Coordinates);
        TransferDamage(uid, folded);
        _audio.PlayPvs(comp.FoldUpSound, folded);
        QueueDel(uid);

        _hands.PickupOrDrop(args.User, folded);
    }

    #endregion

    #region Deploy

    private void OnFoldedAfterInteract(EntityUid uid, HecuTurretFoldedComponent comp, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        args.Handled = true;
        StartDeploy(uid, comp, args.User, args.ClickLocation);
    }

    private void OnFoldedVerbs(EntityUid uid, HecuTurretFoldedComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || args.Hands == null)
            return;

        if (_container.IsEntityInContainer(uid))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("hecu-turret-verb-deploy"),
            Icon = FoldIcon,
            Act = () => StartDeploy(uid, comp, user, Transform(uid).Coordinates),
            Priority = 2,
        });
    }

    private void StartDeploy(EntityUid uid, HecuTurretFoldedComponent comp, EntityUid user, EntityCoordinates location)
    {
        if (!TryGetDeployTile(location, out var tile))
        {
            _popup.PopupEntity(Loc.GetString("hecu-turret-deploy-blocked"), user, user);
            return;
        }

        var ev = new HecuTurretDeployDoAfterEvent(GetNetCoordinates(tile));
        var doAfter = new DoAfterArgs(EntityManager, user, comp.DeployTime, ev, uid, used: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("hecu-turret-deploy-start"), user, user);
    }

    private void OnDeployDoAfter(EntityUid uid, HecuTurretFoldedComponent comp, HecuTurretDeployDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || TerminatingOrDeleted(uid))
            return;

        args.Handled = true;

        // Tile could have been occupied during the do-after
        if (!TryGetDeployTile(GetCoordinates(args.Location), out var tile))
        {
            _popup.PopupEntity(Loc.GetString("hecu-turret-deploy-blocked"), args.User, args.User);
            return;
        }

        var turret = SpawnAtPosition(comp.TurretPrototype, tile);
        TransferDamage(uid, turret);
        _audio.PlayPvs(comp.DeploySound, turret);

        // Head starts facing away from whoever deployed it
        if (TryComp<HecuTurretComponent>(turret, out var turretComp))
        {
            var delta = _transform.GetWorldPosition(turret) - _transform.GetWorldPosition(args.User);
            if (delta.LengthSquared() > 0.0001f)
            {
                turretComp.Rotation = delta.ToWorldAngle() - _transform.GetWorldRotation(turret);
                Dirty(turret, turretComp);
            }
        }

        QueueDel(uid);
    }

    private bool TryGetDeployTile(EntityCoordinates location, out EntityCoordinates tile)
    {
        tile = default;

        var gridUid = _transform.GetGrid(location);
        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        if (!_maps.TryGetTileRef(gridUid.Value, grid, location, out var tileRef))
            return false;

        if (tileRef.Tile.IsEmpty || _turf.IsTileBlocked(tileRef, BlockMask))
            return false;

        tile = _maps.GridTileToLocal(gridUid.Value, grid, tileRef.GridIndices);
        return true;
    }

    #endregion

    private void TransferDamage(EntityUid from, EntityUid to)
    {
        if (!TryComp<DamageableComponent>(from, out var source) ||
            !TryComp<DamageableComponent>(to, out var target))
        {
            return;
        }

        _damageable.SetDamage(to, target, new DamageSpecifier(source.Damage));
    }
}
