using Content.Server.DeviceLinking.Systems;
using Content.Server.Popups;
using Content.Shared._BlackM.Access;
using Content.Shared._BlackM.Access.Systems;
using Robust.Shared.Audio.Systems;

namespace Content.Server._BlackM.Access;

public sealed class AccessButtonSystem : SharedAccessButtonSystem
{
    [Dependency] private readonly DeviceLinkSystem _deviceLink = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    protected override bool TryPress(EntityUid uid, EntityUid card, EntityUid user, AccessButtonComponent component)
    {
        var allowed = IsAllowed(uid, card, component);

        if (!allowed)
        {
            _audio.PlayPvs(component.SoundDeny, uid);
            _popup.PopupEntity(Loc.GetString("access-button-denied"), uid, user);
            return false;
        }

        _audio.PlayPvs(component.SoundAllow, uid);
        _popup.PopupEntity(Loc.GetString("access-button-granted"), uid, user);

        _deviceLink.InvokePort(uid, component.Port);

        return true;
    }
}
