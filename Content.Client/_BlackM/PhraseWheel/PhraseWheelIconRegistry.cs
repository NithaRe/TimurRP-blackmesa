using Robust.Client.Graphics;

namespace Content.Client._BlackM.PhraseWheel;

public static class PhraseWheelIconRegistry
{
    private static readonly TimeSpan MatchWindow = TimeSpan.FromSeconds(1.5);

    private static readonly TimeSpan PendingSoundWindow = TimeSpan.FromSeconds(0.5);

    private static readonly Dictionary<EntityUid, (Texture Texture, TimeSpan Time)> Icons = new();
    private static readonly Dictionary<EntityUid, (Action<Texture> Callback, TimeSpan Time)> Pending = new();

    private static readonly Dictionary<EntityUid, (TimeSpan Duration, TimeSpan Time)> SoundDurations = new();
    private static readonly Dictionary<EntityUid, (Action<TimeSpan> Callback, TimeSpan Time)> PendingSound = new();

    public static void Register(EntityUid uid, Texture texture, TimeSpan now)
    {
        CleanExpired(now);

        if (Pending.Remove(uid, out var pending))
        {
            pending.Callback(texture);
            return;
        }

        Icons[uid] = (texture, now);
    }

    public static Texture? TryTake(EntityUid uid, TimeSpan now, Action<Texture>? callback = null)
    {
        CleanExpired(now);

        if (Icons.Remove(uid, out var entry))
            return entry.Texture;

        if (callback != null)
            Pending[uid] = (callback, now);

        return null;
    }

    public static void RegisterSoundDuration(EntityUid uid, TimeSpan duration, TimeSpan now)
    {
        CleanExpired(now);

        if (PendingSound.Remove(uid, out var pending))
        {
            pending.Callback(duration);
            return;
        }

        SoundDurations[uid] = (duration, now);
    }

    public static TimeSpan? TryTakeSoundDuration(EntityUid uid, TimeSpan now, Action<TimeSpan>? callback = null)
    {
        CleanExpired(now);

        if (SoundDurations.Remove(uid, out var entry))
            return entry.Duration;

        if (callback != null)
            PendingSound[uid] = (callback, now);

        return null;
    }

    public static void Clear()
    {
        Icons.Clear();
        Pending.Clear();
        SoundDurations.Clear();
        PendingSound.Clear();
    }

    private static void CleanExpired(TimeSpan now)
    {
        foreach (var (uid, entry) in Icons)
        {
            if (now - entry.Time > MatchWindow)
                Icons.Remove(uid);
        }

        foreach (var (uid, entry) in Pending)
        {
            if (now - entry.Time > MatchWindow)
                Pending.Remove(uid);
        }

        foreach (var (uid, entry) in SoundDurations)
        {
            if (now - entry.Time > MatchWindow)
                SoundDurations.Remove(uid);
        }

        foreach (var (uid, entry) in PendingSound)
        {
            if (now - entry.Time > PendingSoundWindow)
                PendingSound.Remove(uid);
        }
    }
}
