using System.Linq;
using System.Numerics;
using Content.Server.Inventory;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared._BlackM.CityScanner;
using Content.Shared.Flash;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.CityScanner;

public sealed class CityScannerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedFlashSystem _flash = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly ServerInventorySystem _inventory = default!;

    private static readonly TimeSpan SearchDelay = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan LostInterestCooldown = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CancelCooldown = TimeSpan.FromSeconds(3);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CityScannerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<CityScannerComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextShot = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(3f, 8f));
        ent.Comp.NextIdleSound = _timing.CurTime + RandomIdleDelay(ent.Comp);

        _npc.SetBlackboard(ent.Owner, "FollowRange", 3f);
        _npc.SetBlackboard(ent.Owner, "FollowCloseRange", 1.8f);
        _npc.SetBlackboard(ent.Owner, "IdleRange", 4f);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CityScannerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.State == CityScannerState.Idle && now >= comp.NextIdleSound)
            {
                comp.NextIdleSound = now + RandomIdleDelay(comp);
                _audio.PlayPvs(comp.IdleSound, uid);
            }

            switch (comp.State)
            {
                case CityScannerState.Idle:
                    UpdateIdle(uid, comp, xform, now);
                    break;
                case CityScannerState.Aiming:
                    UpdateAiming(uid, comp, xform, now);
                    break;
                case CityScannerState.Flash:
                    if (now >= comp.StateEnd)
                        Finish(uid, comp, now);
                    break;
            }
        }
    }

    private void UpdateIdle(EntityUid uid, CityScannerComponent comp, TransformComponent xform, TimeSpan now)
    {
        if (comp.Target is { } current)
        {
            if (!IsAlivePlayer(current))
            {
                DropTarget(uid, comp);
            }
            else if (now >= comp.FollowEnd)
            {
                comp.Photographed[current] = now + LostInterestCooldown;
                DropTarget(uid, comp);
            }
        }

        if (now < comp.NextShot)
            return;

        if (comp.Target == null)
        {
            if (now < comp.NextSearch)
                return;

            comp.NextSearch = now + SearchDelay;
            if (FindTarget(uid, comp, xform, now) is not { } found)
                return;

            comp.Target = found;
            comp.FollowEnd = now + comp.FollowTime;
            _npc.SetBlackboard(uid, NPCBlackboard.FollowTarget, new EntityCoordinates(found, Vector2.Zero));
            _audio.PlayPvs(comp.AlertSound, uid);
            DelayIdleSound(comp, now, 4);
        }

        var target = comp.Target.Value;
        if (!_interaction.InRangeUnobstructed(uid, target, comp.PhotoRange))
            return;

        comp.State = CityScannerState.Aiming;
        comp.StateEnd = now + comp.AimTime;
        _appearance.SetData(uid, CityScannerVisuals.State, CityScannerState.Aiming);
        FaceTarget(uid, xform, target);
        _audio.PlayPvs(comp.AimSound, uid);
        _popup.PopupEntity(Loc.GetString("bm-city-scanner-aim",
            ("target", Identity.Entity(target, EntityManager))), uid);
    }

    private void UpdateAiming(EntityUid uid, CityScannerComponent comp, TransformComponent xform, TimeSpan now)
    {
        if (comp.Target is not { } target
            || !IsAlivePlayer(target)
            || !_interaction.InRangeUnobstructed(uid, target, comp.PhotoRange + 1.5f))
        {
            comp.State = CityScannerState.Idle;
            comp.NextShot = now + CancelCooldown;
            _appearance.SetData(uid, CityScannerVisuals.State, CityScannerState.Idle);
            return;
        }

        FaceTarget(uid, xform, target);
        if (now >= comp.StateEnd)
            TakePhoto(uid, comp, xform, target, now);
    }

    private void FaceTarget(EntityUid uid, TransformComponent xform, EntityUid target)
    {
        var dir = _transform.GetWorldPosition(target) - _transform.GetWorldPosition(xform);
        if (dir.LengthSquared() > 0.01f)
            _transform.SetWorldRotation(uid, dir.ToWorldAngle());
    }

    private TimeSpan RandomIdleDelay(CityScannerComponent comp)
    {
        return TimeSpan.FromSeconds(_random.NextFloat((float) comp.MinIdleSound.TotalSeconds,
            (float) comp.MaxIdleSound.TotalSeconds));
    }

    private static void DelayIdleSound(CityScannerComponent comp, TimeSpan now, double seconds)
    {
        var earliest = now + TimeSpan.FromSeconds(seconds);
        if (comp.NextIdleSound < earliest)
            comp.NextIdleSound = earliest;
    }

    private void TakePhoto(EntityUid uid, CityScannerComponent comp, TransformComponent xform, EntityUid target, TimeSpan now)
    {
        _flash.Flash(target, uid, null, comp.FlashDuration, 0.8f, displayPopup: false);
        Spawn(comp.FlashEffect, xform.Coordinates);
        _audio.PlayPvs(comp.ShutterSound, uid);

        var subject = Identity.Name(target, EntityManager);
        var photo = Spawn(comp.PhotoPrototype, xform.Coordinates);
        var data = EnsureComp<ScannerPhotoComponent>(photo);
        data.Subject = subject;
        data.Snapshot = MakeSnapshot(target);
        Dirty(photo, data);

        _meta.SetEntityName(photo, Loc.GetString("bm-scanner-photo-name", ("subject", subject)));
        _throwing.TryThrow(photo, Transform(target).Coordinates, 3f);
        _popup.PopupEntity(Loc.GetString("bm-city-scanner-photo-taken"), target, target, PopupType.Medium);

        comp.Photographed[target] = now + comp.TargetCooldown;
        comp.State = CityScannerState.Flash;
        comp.StateEnd = now + comp.FlashTime;
        _appearance.SetData(uid, CityScannerVisuals.State, CityScannerState.Flash);
    }

    private ScannerSnapshot MakeSnapshot(EntityUid target)
    {
        var snap = new ScannerSnapshot
        {
            Prototype = MetaData(target).EntityPrototype?.ID,
        };

        if (TryComp<HumanoidAppearanceComponent>(target, out var humanoid))
        {
            snap.Species = humanoid.Species;
            snap.Sex = humanoid.Sex;
            snap.Gender = humanoid.Gender;
            snap.Age = humanoid.Age;
            snap.SkinColor = humanoid.SkinColor;
            snap.EyeColor = humanoid.EyeColor;
            snap.Height = humanoid.Height;
            snap.Width = humanoid.Width;
            snap.Markings = new MarkingSet(humanoid.MarkingSet);
            snap.CustomBaseLayers = new(humanoid.CustomBaseLayers);
        }

        var slots = _inventory.GetSlotEnumerator(target);
        while (slots.NextItem(out var item, out var slot))
        {
            // Pocket contents are never visible on the sprite.
            if (slot.Name.StartsWith("pocket", StringComparison.Ordinal))
                continue;

            if (MetaData(item).EntityPrototype is { } proto)
                snap.Equipment[slot.Name] = proto.ID;
        }

        return snap;
    }

    private void Finish(EntityUid uid, CityScannerComponent comp, TimeSpan now)
    {
        var cooldown = _random.NextFloat((float) comp.MinCooldown.TotalSeconds, (float) comp.MaxCooldown.TotalSeconds);
        comp.State = CityScannerState.Idle;
        comp.NextShot = now + TimeSpan.FromSeconds(cooldown);
        _appearance.SetData(uid, CityScannerVisuals.State, CityScannerState.Idle);
        DelayIdleSound(comp, now, 3);
        DropTarget(uid, comp);
        PruneCooldowns(comp, now);
    }

    private EntityUid? FindTarget(EntityUid uid, CityScannerComponent comp, TransformComponent xform, TimeSpan now)
    {
        var origin = _transform.GetWorldPosition(xform);
        EntityUid? best = null;
        var bestDist = float.MaxValue;

        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(xform.Coordinates, comp.SearchRange))
        {
            var target = mob.Owner;
            if (target == uid || !HasComp<ActorComponent>(target) || !_mobState.IsAlive(target, mob.Comp))
                continue;

            if (comp.Photographed.TryGetValue(target, out var until) && now < until)
                continue;

            var dist = (_transform.GetWorldPosition(target) - origin).LengthSquared();
            if (dist >= bestDist || !_interaction.InRangeUnobstructed(uid, target, comp.SearchRange))
                continue;

            best = target;
            bestDist = dist;
        }

        return best;
    }

    private bool IsAlivePlayer(EntityUid target)
    {
        return !TerminatingOrDeleted(target) && HasComp<ActorComponent>(target) && _mobState.IsAlive(target);
    }

    private void DropTarget(EntityUid uid, CityScannerComponent comp)
    {
        comp.Target = null;
        if (TryComp<HTNComponent>(uid, out var htn))
            htn.Blackboard.Remove<EntityCoordinates>(NPCBlackboard.FollowTarget);
    }

    private static void PruneCooldowns(CityScannerComponent comp, TimeSpan now)
    {
        if (comp.Photographed.Count < 16)
            return;

        foreach (var (ent, until) in comp.Photographed.ToArray())
        {
            if (until <= now)
                comp.Photographed.Remove(ent);
        }
    }
}
