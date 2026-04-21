
using Content.Server._BlackM.Economy.PriceDecay;
using Content.Server.Cargo.Components;

namespace Content.Server.Economy.PriceDecay;

public sealed class PriceDecaySystem : EntitySystem
{
    private float _elapsedMinutes = 0f;

    private const float UpdateInterval = 60f;

    private float _timer = 0f;

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
            var t = Math.Min(_elapsedMinutes / decay.TargetMinutes, 1.0);
            var newPrice = decay.InitialPrice - (decay.InitialPrice - decay.MinPrice) * Math.Pow(t, decay.CurvePower);
            price.Price = Math.Max(newPrice, decay.MinPrice);

            Log.Debug($"Цена кристалла: {price.Price:F0} (t={_elapsedMinutes:F1} мин)");
        }
    }
}
