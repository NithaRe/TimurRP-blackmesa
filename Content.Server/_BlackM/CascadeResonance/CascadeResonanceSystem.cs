// SPDX-License-Identifier: AGPL-3.0-or-later
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._BlackM.CascadeResonance;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.Tag;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.CascadeResonance;

public sealed class CascadeResonanceSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedJobSystem _job = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly PointLightSystem _pointLight = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;

    private readonly Dictionary<EntityUid, HashSet<float>> _notifiedCheckpoints = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CascadeResonanceComponent, ActivateInWorldEvent>(OnActivate);
    }

    private void OnActivate(EntityUid uid, CascadeResonanceComponent comp, ActivateInWorldEvent args)
    {
        if (comp.Active) return;
        if (!HasAllowedJob(args.User, comp)) return;

        comp.Active = true;
        comp.Elapsed = 0f;
        comp.DeviceUid = uid;
        _notifiedCheckpoints[uid] = new HashSet<float>();

        _chat.DispatchGlobalAnnouncement(
            Loc.GetString(comp.StartMessage),
            sender: Loc.GetString("cascade-resonance-sender"),
            playSound: false,
            colorOverride: Color.Red);

        if (comp.StartSound != null)
            _audio.PlayGlobal(comp.StartSound, Filter.Broadcast(), true);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<CascadeResonanceComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Active) continue;

            comp.Elapsed += frameTime;
            var remaining = comp.Duration - comp.Elapsed;

            foreach (var checkpoint in new[] { 90f, 60f, 30f })
            {
                if (comp.Elapsed >= comp.Duration - checkpoint && remaining > 0)
                {
                    if (!_notifiedCheckpoints.ContainsKey(uid))
                        _notifiedCheckpoints[uid] = new HashSet<float>();

                    if (_notifiedCheckpoints[uid].Add(checkpoint))
                    {
                        var seconds = (int)remaining;
                        _chat.DispatchGlobalAnnouncement(
                            Loc.GetString(comp.CountdownMessage, ("seconds", seconds)),
                            sender: Loc.GetString("cascade-resonance-sender"),
                            playSound: false,
                            colorOverride: Color.Orange);

                        if (comp.CountdownSound != null)
                            _audio.PlayGlobal(comp.CountdownSound, Filter.Broadcast(), true);
                    }
                }
            }

            foreach (var blinkAt in new[] { 30f, 60f, 90f, 119f })
            {
                if (comp.Elapsed >= blinkAt && comp.Elapsed < blinkAt + frameTime)
                    BlinkLights();
            }

            if (comp.Elapsed >= comp.Duration)
            {
                comp.Active = false;
                _notifiedCheckpoints.Remove(uid);

                _chat.DispatchGlobalAnnouncement(
                    Loc.GetString(comp.CompleteMessage),
                    sender: Loc.GetString("cascade-resonance-sender"),
                    playSound: false,
                    colorOverride: Color.Red);

                if (comp.CompleteSound != null)
                    _audio.PlayGlobal(comp.CompleteSound, Filter.Broadcast(), true);

                TeleportHecu(uid, comp);
            }
        }
    }

    private void BlinkLights()
    {
        var lights = EntityQueryEnumerator<PointLightComponent>();
        while (lights.MoveNext(out var uid, out var light))
        {
            _pointLight.SetEnabled(uid, false, light);
            var capturedUid = uid;
            Timer.Spawn(TimeSpan.FromMilliseconds(500), () =>
            {
                if (Deleted(capturedUid)) return;
                if (TryComp<PointLightComponent>(capturedUid, out var l))
                    _pointLight.SetEnabled(capturedUid, true, l);
            });
        }
    }

    private void TeleportHecu(EntityUid deviceUid, CascadeResonanceComponent comp)
    {
        EntityUid? target = null;
        var tagQuery = EntityQueryEnumerator<TagComponent>();
        while (tagQuery.MoveNext(out var tuid, out _))
        {
            if (_tag.HasTag(tuid, comp.TeleportTargetTag))
            {
                target = tuid;
                break;
            }
        }

        if (target == null) return;

        var targetCoords = Transform(target.Value).Coordinates;

        // Взрыв от самого устройства
        _explosion.QueueExplosion(
            deviceUid,
            "Default",
            totalIntensity: 200,
            slope: 5,
            maxTileIntensity: 2,
            tileBreakScale: 0f,
            maxTileBreak: 0,
            canCreateVacuum: false);

        var mindQuery = EntityQueryEnumerator<MindContainerComponent>();
        while (mindQuery.MoveNext(out var muid, out _))
        {
            if (!_mind.TryGetMind(muid, out var mindId, out _)) continue;
            if (!_job.MindHasJobWithId(mindId, "HECU")) continue;
            _transform.SetCoordinates(muid, targetCoords);
        }
    }

    private bool HasAllowedJob(EntityUid user, CascadeResonanceComponent comp)
    {
        if (!_mind.TryGetMind(user, out var mindId, out _)) return false;
        if (!_job.MindTryGetJobId(mindId, out var jobId)) return false;
        return comp.AllowedJobs.Contains(jobId!.Value.Id);
    }
}