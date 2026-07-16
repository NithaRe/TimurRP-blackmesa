using System;
using System.Collections.Generic;
using Robust.Client.Graphics;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.PhraseWheel;

public static class PhraseWheelIconRegistry
{
    private static readonly Dictionary<EntityUid, (Texture Tex, TimeSpan Expiry)> PendingIcons = new();
    private static readonly Dictionary<EntityUid, (Action<Texture> Callback, TimeSpan Expiry)> PendingCallbacks = new();

    private static readonly TimeSpan MatchWindow = TimeSpan.FromSeconds(1.5);

    public static void Register(EntityUid uid, Texture tex, TimeSpan now)
    {
        if (PendingCallbacks.Remove(uid, out var pending))
        {
            if (pending.Expiry >= now)
            {
                pending.Callback(tex);
                return;
            }
        }

        PendingIcons[uid] = (tex, now + MatchWindow);
    }

    public static Texture? TryTake(EntityUid uid, TimeSpan now, Action<Texture>? onIconReceived = null)
    {
        CleanExpired(now);

        if (PendingIcons.Remove(uid, out var entry))
        {
            return entry.Expiry >= now ? entry.Tex : null;
        }

        if (onIconReceived != null)
        {
            PendingCallbacks[uid] = (onIconReceived, now + MatchWindow);
        }

        return null;
    }

    private static void CleanExpired(TimeSpan now)
    {
        var toRemoveIcons = new List<EntityUid>();
        foreach (var (uid, entry) in PendingIcons)
        {
            if (entry.Expiry < now)
                toRemoveIcons.Add(uid);
        }
        foreach (var uid in toRemoveIcons)
        {
            PendingIcons.Remove(uid);
        }

        var toRemoveCallbacks = new List<EntityUid>();
        foreach (var (uid, entry) in PendingCallbacks)
        {
            if (entry.Expiry < now)
                toRemoveCallbacks.Add(uid);
        }
        foreach (var uid in toRemoveCallbacks)
        {
            PendingCallbacks.Remove(uid);
        }
    }
}
