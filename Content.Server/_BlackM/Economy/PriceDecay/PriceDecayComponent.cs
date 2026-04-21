using Robust.Shared.GameObjects;

namespace Content.Server._BlackM.Economy.PriceDecay;

[RegisterComponent]
public sealed partial class PriceDecayComponent : Component
{
    [DataField]
    public double InitialPrice = 4000;

    [DataField]
    public double MinPrice = 100;

    [DataField]
    public double TargetMinutes = 40;

    [DataField]
    public double CurvePower = 3;
}