using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._BlackM.Events.Fog;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ComplexFogComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan StartTime;

    [DataField, AutoNetworkedField]
    public TimeSpan? EndTime;

    [DataField, AutoNetworkedField]
    public float FadeInTime = 30f;

    [DataField, AutoNetworkedField]
    public float FadeOutTime = 40f;

    [DataField, AutoNetworkedField]
    public float Density = 0.85f;

    [DataField, AutoNetworkedField]
    public Color Color = Color.FromHex("#c3cec6");

    [DataField, AutoNetworkedField]
    public Vector2 WindDirection = new(1f, 0.3f);
}

public static class ComplexFogMath
{
    public static float GetFadeIn(ComplexFogComponent fog, TimeSpan now)
    {
        if (fog.FadeInTime <= 0f)
            return 1f;

        return Math.Clamp((float) (now - fog.StartTime).TotalSeconds / fog.FadeInTime, 0f, 1f);
    }

    public static float GetFadeOut(ComplexFogComponent fog, TimeSpan now)
    {
        if (fog.EndTime is not { } end)
            return 0f;

        if (fog.FadeOutTime <= 0f)
            return 1f;

        return Math.Clamp((float) (now - end).TotalSeconds / fog.FadeOutTime, 0f, 1f);
    }

    public static float GetStrength(ComplexFogComponent fog, TimeSpan now)
    {
        return Smooth(GetFadeIn(fog, now)) * (1f - Smooth(GetFadeOut(fog, now)));
    }

    public static float Smooth(float x)
    {
        x = Math.Clamp(x, 0f, 1f);
        return x * x * (3f - 2f * x);
    }
}
