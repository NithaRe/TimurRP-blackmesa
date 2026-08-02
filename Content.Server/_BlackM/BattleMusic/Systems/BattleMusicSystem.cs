using Content.Shared._BlackM.BattleMusic;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.BattleMusic;

public sealed class BattleMusicSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private const float BattleTimeout = 20f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ActorComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
        SubscribeLocalEvent<BattleMusicComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<BattleMusicComponent, EntityTerminatingEvent>(OnTerminating);
    }

    private void OnMobStateChanged(Entity<BattleMusicComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState is MobState.Dead or MobState.Critical)
            EndBattleFor(ent.Owner, ent.Comp);
    }

    private void OnTerminating(Entity<BattleMusicComponent> ent, ref EntityTerminatingEvent args)
    {
        EndBattleFor(ent.Owner, ent.Comp);
    }

    private void EndBattleFor(EntityUid uid, BattleMusicComponent comp)
    {
        var opponent = comp.Opponent;

        StopBattleMusic(uid, comp);
        RemComp<BattleMusicComponent>(uid);

        if (opponent != null
            && TryComp<BattleMusicComponent>(opponent.Value, out var opponentComp)
            && opponentComp.Opponent == uid)
        {
            StopBattleMusic(opponent.Value, opponentComp);
            RemComp<BattleMusicComponent>(opponent.Value);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var toRemove = new List<EntityUid>();

        var query = EntityQueryEnumerator<BattleMusicComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Opponent == null)
                continue;

            if ((now - comp.LastHitTime).TotalSeconds >= BattleTimeout)
            {
                StopBattleMusic(uid, comp);
                toRemove.Add(uid);
            }
        }

        foreach (var uid in toRemove)
            RemComp<BattleMusicComponent>(uid);
    }

    private void OnBeforeDamageChanged(Entity<ActorComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Origin == null)
            return;

        if (!args.Damage.AnyPositive())
            return;

        var victim = ent.Owner;
        var shooter = args.Origin.Value;

        HandleHit(victim, shooter);
    }

    private void HandleHit(EntityUid victim, EntityUid shooter)
    {
        if (victim == shooter)
            return;

        if (!IsPlayer(victim) || !IsPlayer(shooter))
            return;

        var now = _timing.CurTime;

        if (!TryComp<BattleMusicComponent>(victim, out var victimComp))
            victimComp = AddComp<BattleMusicComponent>(victim);

        victimComp.PendingRetaliation[shooter] = now;

        if (!TryComp<BattleMusicComponent>(shooter, out var shooterComp))
            shooterComp = AddComp<BattleMusicComponent>(shooter);

        if (shooterComp.PendingRetaliation.TryGetValue(victim, out var prevHitTime))
        {
            if ((now - prevHitTime).TotalSeconds <= BattleTimeout)
            {
                StartOrRefreshBattle(victim, victimComp, shooter, now);
                StartOrRefreshBattle(shooter, shooterComp, victim, now);
                return;
            }
        }

        if (victimComp.Opponent == shooter)
            victimComp.LastHitTime = now;
        if (shooterComp.Opponent == victim)
            shooterComp.LastHitTime = now;
    }

    private void StartOrRefreshBattle(EntityUid player, BattleMusicComponent comp, EntityUid opponent, TimeSpan now)
    {
        var isNew = comp.Opponent != opponent;
        comp.Opponent = opponent;
        comp.LastHitTime = now;
        comp.PendingRetaliation.Clear();

        if (isNew)
            SendStart(player, opponent);
    }

    private void StopBattleMusic(EntityUid player, BattleMusicComponent comp)
    {
        comp.Opponent = null;
        comp.PendingRetaliation.Clear();
        SendStop(player);
    }

    private void SendStart(EntityUid player, EntityUid opponent)
    {
        if (!TryGetSession(player, out var session))
            return;

        RaiseNetworkEvent(new BattleMusicStartMessage(), session);

        var playerName = MetaData(player).EntityName;
        var opponentName = MetaData(opponent).EntityName;
        RaiseNetworkEvent(new BattleMusicAnnounceMessage(playerName, opponentName), session);
    }

    private void SendStop(EntityUid player)
    {
        if (TryGetSession(player, out var session))
            RaiseNetworkEvent(new BattleMusicStopMessage(), session);
    }

    private bool TryGetSession(EntityUid uid, out ICommonSession session)
    {
        session = default!;
        if (!TryComp<ActorComponent>(uid, out var actor))
            return false;
        session = actor.PlayerSession;
        return true;
    }

    private bool IsPlayer(EntityUid uid) => HasComp<ActorComponent>(uid);
}