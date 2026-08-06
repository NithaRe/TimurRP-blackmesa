using Content.Shared._BlackM.Events.Gman;
using Content.Shared.Eye;
using Content.Shared.Mobs.Components;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.Events.Gman;

public sealed class GManEventSystem : EntitySystem
{
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly GManWalkerSystem _walker = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;

    private const float EventDuration = 36f;

    public bool StartEvent(ICommonSession target)
    {
        if (target.AttachedEntity is not { } playerUid)
            return false;

        if (!TryComp<MobStateComponent>(playerUid, out _))
            return false;

        if (!_walker.SpawnEncounterForPlayer(playerUid, target, EventDuration))
            return false;

        if (TryComp<EyeComponent>(playerUid, out var eyeComp))
        {
            _eye.SetVisibilityMask(playerUid, eyeComp.VisibilityMask | GManVisibility.Layer, eyeComp);

            Timer.Spawn(TimeSpan.FromSeconds(EventDuration + 0.5f), () =>
            {
                if (TryComp<EyeComponent>(playerUid, out var eyeComp2))
                    _eye.SetVisibilityMask(playerUid, eyeComp2.VisibilityMask & ~GManVisibility.Layer, eyeComp2);
            });
        }

        var duration = TimeSpan.FromSeconds(EventDuration);
        if (!_stun.TryUpdateStunDuration(playerUid, duration))
            _stun.TryAddStunDuration(playerUid, duration);

        _audio.PlayGlobal(
            "/Audio/_BlackM/gman_speech.ogg",
            Filter.SinglePlayer(target),
            true,
            AudioParams.Default.WithVolume(-2f));

        RaiseNetworkEvent(new GManEventStartEvent { Duration = EventDuration }, target);

        return true;
    }
}