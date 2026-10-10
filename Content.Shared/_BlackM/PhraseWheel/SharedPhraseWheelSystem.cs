using Content.Shared.ActionBlocker;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._BlackM.PhraseWheel;

public abstract class SharedPhraseWheelSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;

    public bool IsCategoryAllowed(PhraseWheelComponent comp, ProtoId<PhraseWheelCategoryPrototype> category)
    {
        return comp.AllowedCategories.Count == 0 || comp.AllowedCategories.Contains(category);
    }

    public bool CanAct(EntityUid uid)
    {
        return !TryComp<MobStateComponent>(uid, out var mobState)
               || mobState.CurrentState == MobState.Alive;
    }

    public bool CanSend(EntityUid uid, PhraseWheelEntryPrototype phrase)
    {
        return phrase.ChatType == PhraseWheelChatType.Emote
            ? _actionBlocker.CanEmote(uid)
            : _actionBlocker.CanSpeak(uid);
    }

    public TimeSpan GetCooldownRemaining(PhraseWheelComponent comp)
    {
        var remaining = comp.NextUse - Timing.CurTime;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    public bool CanUse(EntityUid uid, PhraseWheelComponent comp, TimeSpan tolerance = default)
    {
        return CanAct(uid) && GetCooldownRemaining(comp) <= tolerance;
    }

    public void StartCooldown(PhraseWheelComponent comp)
    {
        comp.NextUse = Timing.CurTime + PhraseWheelConstants.UseCooldown;
    }

    public static Color SanitizeColor(Color color)
    {
        color = new Color(color.R, color.G, color.B, 1f);

        for (var i = 0; i < 4 && Luminance(color) < PhraseWheelConstants.MinTextLuminance; i++)
        {
            color = new Color(
                color.R + (1f - color.R) * 0.35f,
                color.G + (1f - color.G) * 0.35f,
                color.B + (1f - color.B) * 0.35f,
                1f);
        }

        return color;
    }

    private static float Luminance(Color c) => 0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B;
}
