namespace Content.Server._BlackM.Underground;

public static class UndergroundLocator
{
    // A short ping near the objective; increasingly slower up to 30 tiles away.
    public static TimeSpan GetInterval(float distance)
    {
        var clamped = Math.Clamp(distance, 0f, 30f);
        return TimeSpan.FromSeconds(0.35 + clamped * (2.15 / 30.0));
    }
}
