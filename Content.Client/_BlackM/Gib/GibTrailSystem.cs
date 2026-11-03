using System.Numerics;
using Content.Shared._BlackM.Gib;
using Robust.Shared.Physics.Components;

namespace Content.Client._BlackM.Gib;

public sealed class GibTrailSystem : EntitySystem
{
    [Dependency] private readonly GibBurstSystem _burst = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private const float Spacing = 0.28f;

    private sealed class TrailState
    {
        public Vector2 Last;
        public float Acc;
    }

    private readonly Dictionary<EntityUid, TrailState> _states = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GibTrailComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<GibTrailComponent> ent, ref ComponentShutdown args)
        => _states.Remove(ent.Owner);

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<GibTrailComponent, TransformComponent, PhysicsComponent>();

        while (query.MoveNext(out var uid, out var trail, out var xform, out var body))
        {
            var pos = _xform.GetWorldPosition(xform);

            if (!_states.TryGetValue(uid, out var st))
            {
                _states[uid] = new TrailState { Last = pos };
                continue;
            }

            var step = (pos - st.Last).Length();
            var dir = step > 0.001f ? (pos - st.Last) / step : Vector2.Zero;
            st.Last = pos;

            if (body.LinearVelocity.LengthSquared() < 0.6f)
            {
                st.Acc = 0f;
                continue;
            }

            st.Acc = MathF.Min(st.Acc + step, 3f);

            while (st.Acc >= Spacing)
            {
                st.Acc -= Spacing;
                _burst.Drip(xform.MapID, pos - dir * st.Acc, trail.Color);
            }
        }
    }
}
