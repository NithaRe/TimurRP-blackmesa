using System.Numerics;

namespace Content.Shared._BlackM.Monorail;

public readonly record struct MonorailTrackEnd(EntityUid Owner, Vector2 Pos, Vector2 Dir);

public readonly record struct MonorailSnapResult(
    Vector2 Position,
    float Rotation,
    Vector2 FinalPosition,
    Vector2 Point,
    float Delta,
    EntityUid Target);

public static class MonorailTrackGeometry
{
    public const float MaxSnapAngle = MathF.PI * 50f / 180f;

    public const float OccupiedTolerance = 0.1f;

    private const float Epsilon = 1e-3f;

    public static List<Vector2> GetLocalPoints(MonorailTrackShape shape)
    {
        switch (shape)
        {
            case MonorailTrackShape.StraightLong:
                return new List<Vector2> { new(0f, -1.5f), new(0f, -0.5f), new(0f, 0.5f), new(0f, 1.5f) };
            case MonorailTrackShape.Corner:
                return Arc(new Vector2(0.5f, -0.5f), 0.5f, 8);
            case MonorailTrackShape.CurveLarge:
                return Arc(new Vector2(1.5f, -1.5f), 2.5f, 20);
            case MonorailTrackShape.End:
                return new List<Vector2> { new(0f, -0.5f), new(0f, 0.2f) };
            default:
                return new List<Vector2> { new(0f, -0.5f), new(0f, 0.5f) };
        }
    }

    public static float LineStep(MonorailTrackShape shape)
    {
        return shape == MonorailTrackShape.StraightLong ? 3f : 1f;
    }

    public static List<Vector2> Arc(Vector2 center, float radius, int segments)
    {
        var list = new List<Vector2>(segments + 1);
        for (var i = 0; i <= segments; i++)
        {
            var a = MathF.PI - MathF.PI / 2f * i / segments;
            list.Add(center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius);
        }

        return list;
    }

    public static Vector2 Rotate(Vector2 v, float angle)
    {
        var (sin, cos) = MathF.SinCos(angle);
        return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }

    public static float WrapPi(float a)
    {
        while (a > MathF.PI)
            a -= MathF.Tau;
        while (a < -MathF.PI)
            a += MathF.Tau;
        return a;
    }

    public static List<Vector2> ToFrame(MonorailTrackShape shape, Vector2 pos, float rot)
    {
        var pts = GetLocalPoints(shape);
        for (var i = 0; i < pts.Count; i++)
            pts[i] = pos + Rotate(pts[i], rot);
        return pts;
    }

    public static List<Vector2> ToFrame(MonorailTrackShape shape, TransformComponent xform)
    {
        return ToFrame(shape, xform.LocalPosition, (float) xform.LocalRotation.Theta);
    }

    public static void AddOpenEnds(EntityUid owner, MonorailTrackShape shape, List<Vector2> pts, List<MonorailTrackEnd> output)
    {
        output.Add(new MonorailTrackEnd(owner, pts[0], Vector2.Normalize(pts[0] - pts[1])));
        if (shape != MonorailTrackShape.End)
            output.Add(new MonorailTrackEnd(owner, pts[^1], Vector2.Normalize(pts[^1] - pts[^2])));
    }

    public static List<MonorailTrackEnd> FreeEnds(List<MonorailTrackEnd> all)
    {
        var free = new List<MonorailTrackEnd>(all.Count);
        var tol2 = OccupiedTolerance * OccupiedTolerance;
        for (var i = 0; i < all.Count; i++)
        {
            var occupied = false;
            for (var j = 0; j < all.Count; j++)
            {
                if (i == j || all[j].Owner == all[i].Owner)
                    continue;

                if ((all[j].Pos - all[i].Pos).LengthSquared() < tol2)
                {
                    occupied = true;
                    break;
                }
            }

            if (!occupied)
                free.Add(all[i]);
        }

        return free;
    }

    public static bool TryFindSnap(
        MonorailTrackShape shape,
        Vector2 pos,
        float rot,
        IReadOnlyList<MonorailTrackEnd> freeEnds,
        float range,
        float maxAngle,
        out MonorailSnapResult result,
        EntityUid ignore = default)
    {
        result = default;
        var local = GetLocalPoints(shape);
        var ends = shape == MonorailTrackShape.End ? 1 : 2;
        var bestScore = float.MaxValue;
        var found = false;

        for (var e = 0; e < ends; e++)
        {
            var endLocal = e == 0 ? local[0] : local[^1];
            var dirLocal = e == 0
                ? Vector2.Normalize(local[0] - local[1])
                : Vector2.Normalize(local[^1] - local[^2]);

            var endPos = pos + Rotate(endLocal, rot);
            var dir = Rotate(dirLocal, rot);
            var have = MathF.Atan2(dir.Y, dir.X);

            foreach (var target in freeEnds)
            {
                if (ignore.IsValid() && target.Owner == ignore)
                    continue;

                var dist = (target.Pos - endPos).Length();
                if (dist > range)
                    continue;

                var wanted = MathF.Atan2(-target.Dir.Y, -target.Dir.X);
                var delta = WrapPi(wanted - have);
                if (MathF.Abs(delta) > maxAngle)
                    continue;

                if (MathF.Abs(delta) < Epsilon)
                    delta = 0f;

                var score = dist + MathF.Abs(delta) * 0.25f;
                if (score >= bestScore)
                    continue;

                bestScore = score;
                var newRot = rot + delta;
                result = new MonorailSnapResult(
                    target.Pos - Rotate(endLocal, rot),
                    newRot,
                    target.Pos - Rotate(endLocal, newRot),
                    target.Pos,
                    delta,
                    target.Owner);
                found = true;
            }
        }

        return found;
    }

    public static Vector2 SnapToStep(Vector2 p, float step)
    {
        if (step <= 0f)
            return p;

        if (MathF.Abs(step - 1f) < Epsilon)
            return new Vector2(MathF.Floor(p.X) + 0.5f, MathF.Floor(p.Y) + 0.5f);

        return new Vector2(MathF.Round(p.X / step) * step, MathF.Round(p.Y / step) * step);
    }

    public static float SnapToCardinal(float rot)
    {
        var quarter = MathF.PI / 2f;
        return MathF.Round(rot / quarter) * quarter;
    }

    public static bool IsAlmostEqual(Vector2 a, Vector2 b)
    {
        return (a - b).LengthSquared() < Epsilon * Epsilon;
    }

    public static float DistanceToPolyline(Vector2 p, List<Vector2> pts, out float along)
    {
        var best = float.MaxValue;
        along = 0f;
        var acc = 0f;
        for (var i = 0; i < pts.Count - 1; i++)
        {
            var a = pts[i];
            var ab = pts[i + 1] - a;
            var len = ab.Length();
            var t = len > 1e-5f ? Math.Clamp(Vector2.Dot(p - a, ab) / (len * len), 0f, 1f) : 0f;
            var d = (a + ab * t - p).Length();
            if (d < best)
            {
                best = d;
                along = acc + len * t;
            }

            acc += len;
        }

        return best;
    }
}
