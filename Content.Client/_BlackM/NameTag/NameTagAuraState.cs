namespace Content.Client._BlackM.NameTag;

public static class NameTagAuraState
{
    public const float Radius = 1.45f;
    public const float AppearTime = 1.1f;

    private static readonly Dictionary<EntityUid, double> Appear = new();

    public static float Touch(EntityUid uid, double t, out float alpha)
    {
        if (!Appear.TryGetValue(uid, out var start))
        {
            start = t;
            Appear[uid] = start;
        }

        return Calc(start, t, out alpha);
    }

    public static bool TryGet(EntityUid uid, double t, out float grow, out float alpha)
    {
        if (!Appear.TryGetValue(uid, out var start))
        {
            grow = 0f;
            alpha = 0f;
            return false;
        }

        grow = Calc(start, t, out alpha);
        return true;
    }

    public static void Forget(EntityUid uid) => Appear.Remove(uid);

    public static IEnumerable<EntityUid> Tracked() => Appear.Keys;

    private static float Calc(double start, double t, out float alpha)
    {
        var p = (float) Math.Clamp((t - start) / AppearTime, 0, 1);
        alpha = Math.Min(1f, p * 3f);
        return EaseOutBack(p);
    }

    public static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        var p = x - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}