using Content.Shared._BlackM.NameTag;

namespace Content.Server._BlackM.NameTag;

public sealed class NameTagSystem : EntitySystem
{
    public void SetTag(
        EntityUid uid,
        string text,
        Color color,
        bool rainbow = true,
        NameTagAuraStyle auraStyle = NameTagAuraStyle.None)
    {
        var comp = EnsureComp<NameTagComponent>(uid);
        comp.Text = text;
        comp.Color = color;
        comp.Rainbow = rainbow;
        comp.AuraStyle = auraStyle;
        Dirty(uid, comp);
    }

    public bool RemoveTag(EntityUid uid)
    {
        return RemComp<NameTagComponent>(uid);
    }
}