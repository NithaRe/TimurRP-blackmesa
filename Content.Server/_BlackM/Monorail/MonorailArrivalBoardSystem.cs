using Content.Shared._BlackM.Monorail;
using Content.Shared.Examine;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.Monorail;

public sealed class MonorailArrivalBoardSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly MonorailSystem _monorail = default!;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan StopSearchInterval = TimeSpan.FromSeconds(10);

    private readonly HashSet<Entity<MonorailStopComponent>> _stops = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MonorailArrivalBoardComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MonorailArrivalBoardComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<MonorailArrivalBoardComponent, ExaminedEvent>(OnExamined);
    }

    private void OnMapInit(Entity<MonorailArrivalBoardComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.Stop = null;
        ent.Comp.NextUpdate = _timing.CurTime;
        ent.Comp.NextStopSearch = TimeSpan.Zero;
    }

    private void OnAnchorChanged(Entity<MonorailArrivalBoardComponent> ent, ref AnchorStateChangedEvent args)
    {
        ent.Comp.Stop = null;
        ent.Comp.NextStopSearch = TimeSpan.Zero;
    }

    private void OnExamined(Entity<MonorailArrivalBoardComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (ent.Comp.Stop == null)
        {
            args.PushMarkup(Loc.GetString("monorail-board-examine-no-stop"));
            return;
        }

        args.PushMarkup(string.IsNullOrWhiteSpace(ent.Comp.StationName)
            ? Loc.GetString("monorail-board-examine-stop")
            : Loc.GetString("monorail-board-examine-stop-named", ("name", ent.Comp.StationName)));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<MonorailArrivalBoardComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var board, out var xform))
        {
            if (now < board.NextUpdate)
                continue;

            board.NextUpdate = now + UpdateInterval;

            if (board.Stop is not { } stopUid || TerminatingOrDeleted(stopUid) || now >= board.NextStopSearch)
            {
                board.Stop = FindStop(uid, board, xform);
                board.NextStopSearch = now + StopSearchInterval;
            }

            var status = MonorailBoardStatus.Offline;
            var time = TimeSpan.Zero;
            string? name = null;

            if (board.Stop is { } stop && TryComp<MonorailStopComponent>(stop, out var stopComp))
            {
                name = string.IsNullOrWhiteSpace(stopComp.StopName) ? null : stopComp.StopName;
                FindBestCar(stop, board.Line ?? stopComp.Line, now, out status, out time);
            }

            SetState(uid, board, status, time, name);
        }
    }

    private EntityUid? FindStop(EntityUid uid, MonorailArrivalBoardComponent board, TransformComponent xform)
    {
        _stops.Clear();
        _lookup.GetEntitiesInRange(xform.Coordinates, board.SearchRange, _stops, LookupFlags.StaticSundries | LookupFlags.Dynamic);

        var origin = _xform.GetWorldPosition(xform);
        EntityUid? best = null;
        var bestDist = float.MaxValue;

        foreach (var stop in _stops)
        {
            if (stop.Owner == uid)
                continue;

            if (board.Line != null && stop.Comp.Line != board.Line)
                continue;

            var d = (_xform.GetWorldPosition(stop.Owner) - origin).LengthSquared();
            if (d >= bestDist)
                continue;

            bestDist = d;
            best = stop.Owner;
        }

        return best;
    }

    private void FindBestCar(EntityUid stop, string line, TimeSpan now,
        out MonorailBoardStatus bestStatus, out TimeSpan bestTime)
    {
        bestStatus = MonorailBoardStatus.Offline;
        bestTime = TimeSpan.Zero;

        var cars = EntityQueryEnumerator<MonorailCarComponent>();
        while (cars.MoveNext(out _, out var car))
        {
            if (car.Line != line)
                continue;

            var index = car.StopEntities.IndexOf(stop);
            if (index < 0)
                continue;

            if (!_monorail.TryGetStopEta(car, index, now, out var status, out var time))
                continue;

            if (Rank(status) < Rank(bestStatus) ||
                Rank(status) == Rank(bestStatus) && time < bestTime)
            {
                bestStatus = status;
                bestTime = time;
            }
        }
    }

    private static int Rank(MonorailBoardStatus status)
    {
        return status switch
        {
            MonorailBoardStatus.Boarding => 0,
            MonorailBoardStatus.Departing => 1,
            MonorailBoardStatus.Arriving => 2,
            _ => 3,
        };
    }

    private void SetState(EntityUid uid, MonorailArrivalBoardComponent board,
        MonorailBoardStatus status, TimeSpan time, string? name)
    {
        if (board.Status == status &&
            board.StationName == name &&
            Math.Abs((board.TargetTime - time).TotalSeconds) < 0.25)
            return;

        board.Status = status;
        board.TargetTime = time;
        board.StationName = name;
        Dirty(uid, board);
    }
}
