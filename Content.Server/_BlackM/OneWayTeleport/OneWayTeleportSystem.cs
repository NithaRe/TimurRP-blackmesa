using System.Linq;
using Content.Server.DoAfter;
using Content.Shared._BlackM.OneWayTeleport;
using Content.Shared.DoAfter;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Localization;

namespace Content.Server._BlackM.OneWayTeleport;

public sealed class OneWayTeleportSystem : EntitySystem
{
    [Dependency] private readonly DoAfterSystem _doAfter     = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedPopupSystem _popup   = default!;

    private const float UpdateInterval = 0.5f;
    private float _timer = 0f;

    public override void Initialize()
    {
        SubscribeLocalEvent<OneWayTeleportComponent, OneWayTeleportDoAfterEvent>(OnDoAfter);
    }

    public override void Update(float frameTime)
    {
        _timer += frameTime;
        if (_timer < UpdateInterval)
            return;
        _timer = 0f;

        var query = EntityQueryEnumerator<OneWayTeleportComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var teleport, out var xform))
        {
            if (!teleport.Enabled)
                continue;

            var nearby = _lookup.GetEntitiesInRange(xform.Coordinates, teleport.Range);

            foreach (var entity in nearby)
            {
                if (!HasComp<MobStateComponent>(entity))
                    continue;

                if (teleport.ActiveDoAfters.Contains(entity))
                    continue;

                if (TryComp<OneWayTeleportUsedComponent>(entity, out var used)
                    && used.UsedIds.Contains(teleport.DestinationId))
                    continue;

                SendWarning(entity, teleport);

                teleport.ActiveDoAfters.Add(entity);

                _doAfter.TryStartDoAfter(new DoAfterArgs(
                    EntityManager,
                    entity,
                    teleport.Delay,
                    new OneWayTeleportDoAfterEvent(),
                    uid,
                    target: uid)
                {
                    BreakOnMove   = true,
                    BreakOnDamage = false,
                    NeedHand      = false,
                });
            }

            var toRemove = teleport.ActiveDoAfters
                .Where(e => !nearby.Contains(e))
                .ToList();
            foreach (var e in toRemove)
                teleport.ActiveDoAfters.Remove(e);
        }
    }

    private void OnDoAfter(EntityUid portalUid, OneWayTeleportComponent teleport, OneWayTeleportDoAfterEvent args)
    {
        teleport.ActiveDoAfters.Remove(args.User);

        if (args.Cancelled || args.Handled)
            return;

        var dest = FindDestination(teleport.DestinationId);
        if (dest == null)
        {
            Log.Error($"OneWayTeleport: маркер назначения '{teleport.DestinationId}' не найден!");
            return;
        }

        _transform.SetCoordinates(args.User, Transform(dest.Value).Coordinates);

        var used = EnsureComp<OneWayTeleportUsedComponent>(args.User);
        used.UsedIds.Add(teleport.DestinationId);

        args.Handled = true;
    }

    public int SetGroupEnabled(string destinationId, bool enabled)
    {
        var count = 0;

        var query = EntityQueryEnumerator<OneWayTeleportComponent>();
        while (query.MoveNext(out _, out var teleport))
        {
            if (teleport.DestinationId != destinationId)
                continue;

            teleport.Enabled = enabled;
            count++;
        }

        return count;
    }

    private EntityUid? FindDestination(string destinationId)
    {
        var query = EntityQueryEnumerator<OneWayTeleportDestinationComponent>();
        while (query.MoveNext(out var uid, out var dest))
        {
            if (dest.DestinationId == destinationId)
                return uid;
        }
        return null;
    }

    private void SendWarning(EntityUid entity, OneWayTeleportComponent teleport)
    {
        var seconds = (int)teleport.Delay;

        string message;
        if (Loc.TryGetString(teleport.WarningMessage, out var localized, ("seconds", seconds)))
        {
            message = localized;
        }
        else
        {
            message =
                $"[ВНИМАНИЕ] Через {seconds} сек. вы будете опущены на нижний этаж комплекса. Вернуться будет НЕВОЗМОЖНО. Отойдите чтобы отменить.";
        }

        _popup.PopupEntity(message, entity, entity, PopupType.LargeCaution);
    }
}
