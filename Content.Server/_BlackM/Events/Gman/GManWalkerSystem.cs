using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared._BlackM.Events.Gman;
using Content.Shared.Eye;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.Events.Gman;

public sealed class GManWalkerSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly VisibilitySystem _visibility = default!;

    private const string WalkerProto = "BMGManWalker";
    private const string VoidProto = "BMGManVoidRect";

    private const float AppearDistance = 2f;
    private const float DoorExtraDistance = 1.4f;

    private const float ExitWalkDuration = 1.5f;
    private const float DoorFadeTime = 1.2f;

    private const float EndSafetyBuffer = 0.5f;

    private sealed class Encounter
    {
        public EntityUid Walker;
        public EntityUid Door;
        public Vector2 StandPos;
        public Vector2 DoorPos;
        public Angle ExitFacing;
        public float Elapsed;
        public float ExitStartTime;
        public float ExitEndTime;
        public ICommonSession Session = default!;
    }

    private readonly List<Encounter> _encounters = new();

    public bool SpawnEncounterForPlayer(EntityUid playerUid, ICommonSession session, float eventDuration)
    {
        if (!TryComp<TransformComponent>(playerUid, out var xform) || xform.MapUid is not { } mapUid)
            return false;

        var exitStart = Math.Max(1f, eventDuration - ExitWalkDuration - DoorFadeTime - EndSafetyBuffer);
        var exitEnd = exitStart + ExitWalkDuration;

        var playerPos = _xform.GetWorldPosition(playerUid);
        var angle = _random.NextAngle();
        var dir = angle.ToWorldVec();

        var standPos = playerPos + dir * AppearDistance;
        var doorPos = playerPos + dir * (AppearDistance + DoorExtraDistance);
        var standFacing = Angle.FromWorldVec(-dir);
        var exitFacing = Angle.FromWorldVec(dir);

        EntityUid walker;
        EntityUid door;
        try
        {
            walker = Spawn(WalkerProto, new EntityCoordinates(mapUid, standPos));
            door = Spawn(VoidProto, new EntityCoordinates(mapUid, doorPos));

            var walkerVis = EnsureComp<VisibilityComponent>(walker);
            _visibility.SetLayer((walker, walkerVis), GManVisibility.Layer);

            var doorVis = EnsureComp<VisibilityComponent>(door);
            _visibility.SetLayer((door, doorVis), GManVisibility.Layer);
        }
        catch (Exception)
        {
            return false;
        }

        _xform.SetWorldPositionRotation(walker, standPos, standFacing);
        _xform.SetWorldRotation(door, Angle.Zero);

        _encounters.Add(new Encounter
        {
            Walker = walker,
            Door = door,
            StandPos = standPos,
            DoorPos = doorPos,
            ExitFacing = exitFacing,
            ExitStartTime = exitStart,
            ExitEndTime = exitEnd,
            Session = session,
        });

        return true;
    }

    public void ForceStopAll()
    {
        foreach (var e in _encounters)
        {
            if (Exists(e.Walker))
                QueueDel(e.Walker);
            if (Exists(e.Door))
                QueueDel(e.Door);
        }

        _encounters.Clear();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        for (var i = _encounters.Count - 1; i >= 0; i--)
        {
            var e = _encounters[i];

            if (!Exists(e.Walker))
            {
                _encounters.RemoveAt(i);
                continue;
            }

            e.Elapsed += frameTime;

            if (e.Elapsed < e.ExitStartTime)
                continue;

            if (e.Elapsed >= e.ExitEndTime)
            {
                QueueDel(e.Walker);

                if (Exists(e.Door))
                {
                    var netEnt = GetNetEntity(e.Door);
                    RaiseNetworkEvent(new GManDoorFadeEvent { Door = netEnt, FadeTime = DoorFadeTime }, e.Session);

                    var door = e.Door;
                    Timer.Spawn(TimeSpan.FromSeconds(DoorFadeTime + 0.2f), () =>
                    {
                        if (Exists(door))
                            QueueDel(door);
                    });
                }

                _encounters.RemoveAt(i);
                continue;
            }

            var t = (e.Elapsed - e.ExitStartTime) / ExitWalkDuration;
            var pos = Vector2.Lerp(e.StandPos, e.DoorPos, Math.Clamp(t, 0f, 1f));
            _xform.SetWorldPositionRotation(e.Walker, pos, e.ExitFacing);
        }
    }
}