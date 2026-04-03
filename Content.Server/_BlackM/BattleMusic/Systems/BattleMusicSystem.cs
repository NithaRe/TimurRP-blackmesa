using Content.Shared._BlackM.BattleMusic;
using Content.Shared.Damage;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.BattleMusic;

public sealed class BattleMusicSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly new ISawmill Log = Logger.GetSawmill("battle_music");

    private const float BattleTimeout = 20f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ActorComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
        Log.Info("BattleMusicSystem initialized");
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
                Log.Info($"Timeout for {uid}");
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

        Log.Info($"Combat damage: victim={victim} origin={shooter} types={string.Join(",", args.Damage.DamageDict.Keys)}");

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
                Log.Info($"Mutual hit! {victim} vs {shooter}");
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
            SendStart(player);
    }

    private void StopBattleMusic(EntityUid player, BattleMusicComponent comp)
    {
        comp.Opponent = null;
        comp.PendingRetaliation.Clear();
        SendStop(player);
    }

    private void SendStart(EntityUid player)
    {
        if (TryGetSession(player, out var session))
        {
            Log.Info($"StartMessage → {session.Name}");
            RaiseNetworkEvent(new BattleMusicStartMessage(), session);
        }
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