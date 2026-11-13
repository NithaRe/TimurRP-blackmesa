using Content.Shared.Zombies;

#pragma warning disable IDE0130
namespace Content.Server.Zombies;

public sealed partial class ZombieSystem
{
    private static readonly float[] XenBiteChances = { 0.03f, 0.07f, 0.12f, 0.15f, 0.20f };
    private static readonly TimeSpan XenBiteResetDelay = TimeSpan.FromMinutes(1);

    private void UpdateXenBites(TimeSpan now)
    {
        var query = EntityQueryEnumerator<XenBiteExposureComponent>();
        while (query.MoveNext(out _, out var exposure))
        {
            if (now >= exposure.ResetAt)
                exposure.Bites = 0;
        }
    }

    private float GetXenBiteInfectionChance(EntityUid target, ZombieComponent zombie)
    {
        var exposure = EnsureComp<XenBiteExposureComponent>(target);
        var now = _timing.CurTime;
        if (now >= exposure.ResetAt)
            exposure.Bites = 0;

        var bites = Math.Clamp(exposure.Bites, 0, XenBiteChances.Length - 1);
        var chance = GetZombieInfectionChance(target, zombie, XenBiteChances[bites]);

        // Fully protective equipment must not accumulate infectious bites.
        if (chance > 0f)
        {
            exposure.Bites = Math.Min(bites + 1, XenBiteChances.Length);
            exposure.ResetAt = now + XenBiteResetDelay;
        }

        return chance;
    }
}
