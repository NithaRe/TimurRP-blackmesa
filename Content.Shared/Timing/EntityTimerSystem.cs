using System.Threading;
using Robust.Shared.Collections;
using Robust.Shared.Exceptions;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Shared.Timing;

[RegisterComponent, ComponentProtoName("Timer")]
public sealed partial class EntityTimerComponent : Component
{
    internal readonly List<(Timer Timer, CancellationToken Cancellation)> Timers = new();
}

/// <summary>
/// Schedules callbacks on entities. Pausing or deleting an entity also pauses or removes its timers.
/// </summary>
public sealed partial class EntityTimerSystem : EntitySystem
{
    [Dependency] private IRuntimeLog _runtimeLog = default!;

    public void Schedule(
        EntityUid uid,
        TimeSpan delay,
        Action callback,
        bool repeating = false,
        CancellationToken cancellation = default)
    {
        var timers = EnsureComp<EntityTimerComponent>(uid).Timers;
        timers.Add((new Timer((int) delay.TotalMilliseconds, repeating, callback), cancellation));
    }

    public override void Update(float frameTime)
    {
        // Timer callbacks may spawn or delete entities.
        var active = new ValueList<Entity<EntityTimerComponent>>();
        var query = EntityQueryEnumerator<EntityTimerComponent>();
        while (query.MoveNext(out var uid, out var component))
            active.Add((uid, component));

        foreach (var entity in active)
        {
            var component = entity.Comp;
            if (component.Deleted || TerminatingOrDeleted(entity.Owner))
                continue;

            var timers = component.Timers;
            var count = timers.Count;
            for (var i = 0; i < count && !component.Deleted && !TerminatingOrDeleted(entity.Owner); i++)
            {
                var (timer, cancellation) = timers[i];
                if (!cancellation.IsCancellationRequested)
                    timer.Update(frameTime, _runtimeLog);
            }

            timers.RemoveAll(entry => !entry.Timer.IsActive || entry.Cancellation.IsCancellationRequested);
            if (timers.Count == 0 && !component.Deleted && !TerminatingOrDeleted(entity.Owner))
                RemComp<EntityTimerComponent>(entity.Owner);
        }
    }
}
