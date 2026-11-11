using Content.Shared._BlackM.Monorail;

namespace Content.Server._BlackM.Monorail;

public sealed partial class MonorailSystem
{
    public bool TryGetStopEta(MonorailCarComponent car, int target, TimeSpan now,
        out MonorailBoardStatus status, out TimeSpan time)
    {
        status = MonorailBoardStatus.Offline;
        time = TimeSpan.Zero;

        var count = car.Stops.Count;
        if (car.State == MonorailCarState.Uninitialized || count == 0 || target < 0 || target >= count)
            return false;

        if (car.State == MonorailCarState.Dwelling && car.StopIndex == target)
        {
            status = car.DoorsClosed ? MonorailBoardStatus.Departing : MonorailBoardStatus.Boarding;
            time = car.NextDeparture;
            return true;
        }

        var index = car.StopIndex;
        var dir = car.Direction;
        double t;

        if (car.State == MonorailCarState.Moving)
        {
            var arrive = Math.Max(0.0, (car.TripStart - now).TotalSeconds + car.TripDuration);
            if (index == target)
            {
                status = MonorailBoardStatus.Arriving;
                time = now + TimeSpan.FromSeconds(arrive);
                return true;
            }

            t = arrive + car.DwellTime;
        }
        else
        {
            t = Math.Max(0.0, (car.NextDeparture - now).TotalSeconds);
        }

        for (var i = 0; i < count * 2 + 2; i++)
        {
            var next = PeekNextStop(index, ref dir, count, car.Loop);
            float dist;
            if (car.Loop)
            {
                dist = (car.Stops[next] - car.Stops[index]) % car.Length;
                if (dist < 0.05f)
                    dist += car.Length;
            }
            else
            {
                dist = MathF.Abs(car.Stops[next] - car.Stops[index]);
            }

            t += TripDuration(dist, car.MaxSpeed, car.Acceleration);
            if (next == target)
            {
                status = MonorailBoardStatus.Arriving;
                time = now + TimeSpan.FromSeconds(t);
                return true;
            }

            t += car.DwellTime;
            index = next;
        }

        return false;
    }

    private static int PeekNextStop(int index, ref int dir, int count, bool loop)
    {
        if (loop)
            return (index + 1) % count;

        var next = index + dir;
        if (next < 0 || next >= count)
        {
            dir = -dir;
            next = index + dir;
        }

        return Math.Clamp(next, 0, count - 1);
    }
}
