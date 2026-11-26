using System.Collections.Generic;
using Content.Shared._BlackM.PhraseWheel;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.Shop;

public sealed class ShopEntitlementsSystem : EntitySystem
{
    [Dependency] private ShopEntitlementsManager _shop = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan PurgeInterval = TimeSpan.FromSeconds(15);
    private TimeSpan _nextPurge;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
        _shop.Changed += ApplyAll;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _shop.Changed -= ApplyAll;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextPurge)
            return;

        _nextPurge = _timing.CurTime + PurgeInterval;
        _shop.PurgeExpired();
    }

    private void OnAttached(PlayerAttachedEvent ev) => Apply(ev.Player, ev.Entity);

    private void OnSpawnComplete(PlayerSpawnCompleteEvent ev) => Apply(ev.Player, ev.Mob);

    private void ApplyAll()
    {
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is { } uid)
                Apply(session, uid);
        }
    }

    private void Apply(ICommonSession session, EntityUid uid)
    {
        if (TerminatingOrDeleted(uid) || HasComp<GhostComponent>(uid))
            return;

        var desired = new HashSet<ProtoId<PhraseWheelCategoryPrototype>>();
        foreach (var category in _shop.GetPhraseCategories(session.UserId))
        {
            if (_proto.HasIndex<PhraseWheelCategoryPrototype>(category))
                desired.Add(category);
        }

        TryComp<ShopPhraseWheelGrantComponent>(uid, out var grant);

        if (!TryComp<PhraseWheelComponent>(uid, out var wheel))
        {
            if (desired.Count == 0)
            {
                if (grant != null)
                    RemComp(uid, grant);
                return;
            }

            grant = EnsureComp<ShopPhraseWheelGrantComponent>(uid);
            grant.Added.Clear();
            grant.OwnsWheel = true;
            wheel = EnsureComp<PhraseWheelComponent>(uid);
            wheel.AllowedCategories.Clear();
        }
        else if (grant == null)
        {
            if (desired.Count == 0 || wheel.AllowedCategories.Count == 0)
                return;

            grant = AddComp<ShopPhraseWheelGrantComponent>(uid);
        }

        var result = new HashSet<ProtoId<PhraseWheelCategoryPrototype>>(wheel.AllowedCategories);
        foreach (var added in grant.Added)
            result.Remove(added);

        grant.Added.Clear();
        foreach (var category in desired)
        {
            if (result.Add(category))
                grant.Added.Add(category);
        }

        if (result.Count == 0 && grant.OwnsWheel)
        {
            RemComp(uid, wheel);
            RemComp(uid, grant);
            return;
        }

        if (grant.Added.Count == 0 && !grant.OwnsWheel)
            RemComp(uid, grant);

        if (result.SetEquals(wheel.AllowedCategories))
            return;

        wheel.AllowedCategories = result;
        Dirty(uid, wheel);
    }
}
