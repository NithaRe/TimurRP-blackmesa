using Content.Server._BlackM.Economy.PriceDecay;
using Content.Server.Cargo.Components;
using Content.Shared.GameTicking;

namespace Content.Server.Economy.PriceDecay;

public sealed class PriceDecaySystem : EntitySystem
{
    private float _elapsedMinutes = 0f;

    private const float UpdateInterval = 60f;

    private float _timer = 0f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PriceDecayComponent, ComponentInit>(OnComponentInit);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnComponentInit(EntityUid uid, PriceDecayComponent decay, ComponentInit args)
    {
        if (!TryComp<StaticPriceComponent>(uid, out var price))
            return;

        price.Price = CalculatePrice(decay);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _elapsedMinutes = 0f;
        _timer = 0f;
    }

    private double CalculatePrice(PriceDecayComponent decay)
    {
        var t = Math.Min(_elapsedMinutes / decay.TargetMinutes, 1.0);
        var newPrice = decay.InitialPrice - (decay.InitialPrice - decay.MinPrice) * Math.Pow(t, decay.CurvePower);
        return Math.Max(newPrice, decay.MinPrice);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _timer += frameTime;
        _elapsedMinutes += frameTime / 60f;

        if (_timer < UpdateInterval)
            return;

        _timer = 0f;

        var query = EntityQueryEnumerator<PriceDecayComponent, StaticPriceComponent>();
        while (query.MoveNext(out _, out var decay, out var price))
        {
            price.Price = CalculatePrice(decay);

            Log.Debug($"Цена кристалла: {price.Price:F0} (t={_elapsedMinutes:F1} мин)");
        }
    }
}