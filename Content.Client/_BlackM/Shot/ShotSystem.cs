using System.Numerics;
using Content.Shared._BlackM.Shot;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.Shot;

public sealed class ShotSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private const string MuzzleLight = "BlackMMuzzleLight";
    private const string ImpactLight = "BlackMImpactLight";

    private readonly Dictionary<EntityUid, ShotState> _states = new();
    private ShotOverlay _overlay = default!;

    private TimeSpan _lastMuzzle;
    private Vector2 _lastMuzzlePos;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new ShotOverlay(_cache, _states);
        _overlays.AddOverlay(_overlay);
        SubscribeLocalEvent<ShotComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_overlay);
        _states.Clear();
    }

    public override void FrameUpdate(float frameTime)
    {
        var cur = _timing.CurTime;
        var query = EntityQueryEnumerator<ShotComponent, TransformComponent, PhysicsComponent>();

        while (query.MoveNext(out var uid, out var shot, out var xform, out var body))
        {
            if (body.LinearVelocity.LengthSquared() < 9f)
                continue;

            var pos = _xform.GetWorldPosition(xform);
            var dir = _xform.GetWorldRotation(xform).ToWorldVec();

            if (!_states.TryGetValue(uid, out var st))
            {
                st = new ShotState
                {
                    Comp = shot, Map = xform.MapID, Pos = pos, Dir = dir,
                    Visible = _random.Prob(shot.TracerChance),
                    Phase = _random.NextFloat(6.28f),
                };
                _states[uid] = st;

                if (shot.Muzzle)
                    TryMuzzle(st, cur);
            }

            st.Dist += (pos - st.Pos).Length();
            st.Pos = pos;
            st.Dir = dir;
            st.Age += frameTime;
        }
    }

    private void TryMuzzle(ShotState st, TimeSpan cur)
    {
        if (cur - _lastMuzzle < TimeSpan.FromMilliseconds(70) && (st.Pos - _lastMuzzlePos).LengthSquared() < 1f)
            return;

        _lastMuzzle = cur;
        _lastMuzzlePos = st.Pos;

        _overlay.Muzzle(st.Map, st.Pos, st.Dir, st.Comp);

        if (st.Comp.Light)
            Spawn(MuzzleLight, new MapCoordinates(st.Pos + st.Dir * 0.5f, st.Map));
    }

    private void OnShutdown(Entity<ShotComponent> ent, ref ComponentShutdown args)
    {
        if (!_states.Remove(ent.Owner, out var st))
            return;

        if (!ent.Comp.Impact || st.Dist < 0.6f || st.Age > 4f)
            return;

        _overlay.Impact(st.Map, st.Pos, st.Dir, ent.Comp);

        if (ent.Comp.Light)
            Spawn(ImpactLight, new MapCoordinates(st.Pos, st.Map));
    }
}
