using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Turret;

[Serializable, NetSerializable]
public enum HecuTurretState : byte
{
    Idle,
    Preparing,
    Armed,
}

public enum HecuTurretLayers : byte
{
    Base,
    Head,
}

public static class HecuTurretMath
{
    public static double Normalize(double theta)
    {
        theta %= Math.Tau;
        if (theta > Math.PI)
            theta -= Math.Tau;
        else if (theta < -Math.PI)
            theta += Math.Tau;

        return theta;
    }

    public static double Distance(Angle a, Angle b)
    {
        return Math.Abs(Normalize(b.Theta - a.Theta));
    }

    public static Angle RotateTowards(Angle current, Angle goal, double maxStep)
    {
        var diff = Normalize(goal.Theta - current.Theta);
        if (Math.Abs(diff) <= maxStep)
            return new Angle(Normalize(goal.Theta));

        return new Angle(Normalize(current.Theta + Math.Sign(diff) * maxStep));
    }

    public static Angle Spin(Angle current, double step)
    {
        return new Angle(Normalize(current.Theta + step));
    }
}
