using Content.Shared.Examine;
using Robust.Shared.Timing;

namespace Content.Shared._BlackM.Expedition;

public sealed class ExpeditionPortalExamineSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ExpeditionPortalComponent, ExaminedEvent>(OnExamined);
    }

    public static string FormatTime(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
            time = TimeSpan.Zero;
        return $"{(int) time.TotalMinutes}:{time.Seconds:D2}";
    }

    private void OnExamined(Entity<ExpeditionPortalComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var comp = ent.Comp;

        if (!comp.Active || comp.ClosesAt is not { } closes)
        {
            args.PushMarkup(Loc.GetString("expedition-portal-examine-inactive"));
            return;
        }

        args.PushMarkup(Loc.GetString("expedition-portal-examine-open",
            ("time", FormatTime(closes - _timing.CurTime))));

        if (comp.OpenedAt is { } opened)
        {
            args.PushMarkup(Loc.GetString("expedition-portal-examine-opened",
                ("time", FormatTime(_timing.CurTime - opened))));
        }
    }
}
