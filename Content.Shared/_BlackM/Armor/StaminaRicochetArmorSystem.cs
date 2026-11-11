using Content.Shared._Goobstation.Wizard.Projectiles;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Administration.Logs;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;

namespace Content.Shared._BlackM.Armor;

public sealed class StaminaRicochetArmorSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.SubscribeWithRelay<StaminaRicochetArmorComponent, ProjectileReflectAttemptEvent>(OnProjectileReflectAttempt, baseEvent: false, held: false);
        Subs.SubscribeWithRelay<StaminaRicochetArmorComponent, HitScanReflectAttemptEvent>(OnHitscanReflectAttempt, baseEvent: false, held: false);
        SubscribeLocalEvent<StaminaRicochetArmorComponent, ExaminedEvent>(OnExamined);
    }

    #region Chance

    public float GetRicochetChance(StaminaRicochetArmorComponent comp, EntityUid? wearer)
    {
        var staminaFraction = 1f;

        if (wearer != null && TryComp<StaminaComponent>(wearer.Value, out var stamina))
        {
            if (stamina.Critical && comp.CritAlwaysPenetrates)
                return 0f;

            if (stamina.CritThreshold > 0f)
            {
                var damage = _stamina.GetStaminaDamage(wearer.Value, stamina);
                staminaFraction = 1f - Math.Clamp(damage / stamina.CritThreshold, 0f, 1f);
            }
        }

        var curve = MathF.Pow(staminaFraction, MathF.Max(comp.StaminaExponent, 0.01f));
        var chance = comp.MinRicochetChance + (comp.MaxRicochetChance - comp.MinRicochetChance) * curve;
        return Math.Clamp(chance, 0f, 1f);
    }

    private bool TryGetWearer(Entity<StaminaRicochetArmorComponent> armor, out EntityUid wearer)
    {
        wearer = default;

        if (!_inventory.TryGetContainingSlot(armor.Owner, out var slot)
            || (slot.SlotFlags & armor.Comp.SlotFlags) == 0)
            return false;

        wearer = Transform(armor).ParentUid;
        return wearer.IsValid();
    }

    private bool IsProtectedPart(StaminaRicochetArmorComponent comp, EntityUid? shooter)
    {
        if (comp.ProtectedParts == null)
            return true;

        if (shooter == null || !TryComp<TargetingComponent>(shooter.Value, out var targeting))
            return true;
        return (comp.ProtectedParts.Value & targeting.Target) != 0;
    }

    #endregion

    #region Projectiles

    private void OnProjectileReflectAttempt(Entity<StaminaRicochetArmorComponent> ent, ref ProjectileReflectAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryGetWearer(ent, out var wearer))
            return;

        var projectile = args.ProjUid;
        if (!TryComp<ReflectiveComponent>(projectile, out var reflective)
            || (ent.Comp.Reflects & reflective.Reflective) == 0x0
            || !TryComp<PhysicsComponent>(projectile, out var physics))
            return;

        var shooter = args.Component.Shooter;
        if (!IsProtectedPart(ent.Comp, shooter))
            return;

        if (!_random.Prob(GetRicochetChance(ent.Comp, wearer)))
        {
            OnPenetrated(ent.Comp, wearer);
            return;
        }

        var rotation = _random.NextAngle(-ent.Comp.Spread / 2, ent.Comp.Spread / 2).Opposite();
        var existingVelocity = _physics.GetMapLinearVelocity(projectile, component: physics);
        var relativeVelocity = existingVelocity - _physics.GetMapLinearVelocity(wearer);
        var newVelocity = rotation.RotateVec(relativeVelocity);
        var difference = newVelocity - existingVelocity;

        _physics.SetLinearVelocity(projectile, physics.LinearVelocity + difference, body: physics);

        var locRot = Transform(projectile).LocalRotation;
        var newRot = rotation.RotateVec(locRot.ToVec());
        _transform.SetLocalRotation(projectile, newRot.ToAngle());

        if (TryComp(projectile, out HomingProjectileComponent? homing))
            RemCompDeferred(projectile, homing);

        _adminLogger.Add(LogType.BulletHit, LogImpact.Medium,
            $"{ToPrettyString(wearer)}'s armor {ToPrettyString(ent.Owner)} ricocheted {ToPrettyString(projectile)} shot by {ToPrettyString(shooter)}");

        args.Component.Shooter = wearer;
        args.Component.Weapon = wearer;
        Dirty(projectile, args.Component);

        OnRicochet(ent.Comp, wearer, shooter, projectile);
        args.Cancelled = true;
    }

    #endregion

    #region Hitscan

    private void OnHitscanReflectAttempt(Entity<StaminaRicochetArmorComponent> ent, ref HitScanReflectAttemptEvent args)
    {
        if (args.Reflected)
            return;

        if ((ent.Comp.Reflects & args.Reflective) == 0x0)
            return;

        if (!TryGetWearer(ent, out var wearer))
            return;

        if (!IsProtectedPart(ent.Comp, args.Shooter))
            return;

        if (!_random.Prob(GetRicochetChance(ent.Comp, wearer)))
        {
            OnPenetrated(ent.Comp, wearer);
            return;
        }

        var spread = _random.NextAngle(-ent.Comp.Spread / 2, ent.Comp.Spread / 2);
        args.Direction = -spread.RotateVec(args.Direction);
        args.Reflected = true;

        _adminLogger.Add(LogType.HitScanHit, LogImpact.Medium,
            $"{ToPrettyString(wearer)}'s armor {ToPrettyString(ent.Owner)} ricocheted hitscan from {ToPrettyString(args.SourceItem)} shot by {ToPrettyString(args.Shooter)}");

        OnRicochet(ent.Comp, wearer, args.Shooter, args.SourceItem);
    }

    #endregion

    #region Effects

    private void OnRicochet(StaminaRicochetArmorComponent comp, EntityUid wearer, EntityUid? shooter, EntityUid? with)
    {
        if (comp.StaminaDamagePerRicochet > 0f)
        {
            _stamina.TakeStaminaDamage(wearer, comp.StaminaDamagePerRicochet,
                source: shooter, with: with, applyResistances: comp.ApplyStaminaResistance);
        }

        if (!_net.IsServer)
            return;

        _popup.PopupEntity(Loc.GetString(comp.RicochetPopup), wearer, PopupType.Small);
        _audio.PlayPvs(comp.SoundOnRicochet, wearer);
    }

    private void OnPenetrated(StaminaRicochetArmorComponent comp, EntityUid wearer)
    {
        if (!_net.IsServer)
            return;

        _popup.PopupEntity(Loc.GetString(comp.PenetratePopup), wearer, PopupType.SmallCaution);
        _audio.PlayPvs(comp.SoundOnPenetrate, wearer);
    }

    #endregion

    #region Examine

    private void OnExamined(Entity<StaminaRicochetArmorComponent> ent, ref ExaminedEvent args)
    {
        if (!ent.Comp.Examinable)
            return;

        using (args.PushGroup(nameof(StaminaRicochetArmorComponent)))
        {
            args.PushMarkup(Loc.GetString("blackops-armor-ricochet-examine",
                ("max", MathF.Round(ent.Comp.MaxRicochetChance * 100f)),
                ("min", MathF.Round(ent.Comp.MinRicochetChance * 100f))));

            if (args.IsInDetailsRange && TryGetWearer(ent, out var wearer))
            {
                args.PushMarkup(Loc.GetString("blackops-armor-ricochet-examine-current",
                    ("chance", MathF.Round(GetRicochetChance(ent.Comp, wearer) * 100f))));
            }
        }
    }

    #endregion
}
