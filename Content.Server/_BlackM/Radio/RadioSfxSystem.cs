using Content.Server.Radio;
using Content.Shared._BlackM.CCVar;
using Content.Shared._BlackM.Radio;
using Content.Shared.Radio.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.Radio;

public sealed class RadioSfxSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPlayerManager _players = default!;

    private readonly Dictionary<NetUserId, float> _volume = new();

    private readonly List<PendingEndClick> _pending = new();
    private bool _enabled = true;

    private bool _transmissionDistorted;

    private readonly record struct PendingEndClick(TimeSpan At, EntityUid Device, EntityUid Speaker);

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, BlackMCVars.RadioSfxEnabled, value => _enabled = value, true);

        SubscribeLocalEvent<HeadsetComponent, MapInitEvent>(OnHeadsetMapInit);
        SubscribeLocalEvent<RadioSfxComponent, RadioReceiveEvent>(OnReceive);

        SubscribeNetworkEvent<RadioSfxVolumeEvent>(OnVolumeMessage);
        _players.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _players.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnVolumeMessage(RadioSfxVolumeEvent msg, EntitySessionEventArgs args)
    {
        _volume[args.SenderSession.UserId] = float.IsFinite(msg.Gain) ? Math.Clamp(msg.Gain, 0f, 1f) : 1f;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus == SessionStatus.Disconnected)
            _volume.Remove(e.Session.UserId);
    }

    private void OnHeadsetMapInit(EntityUid uid, HeadsetComponent component, MapInitEvent args)
    {
        EnsureComp<RadioSfxComponent>(uid);
    }

    public void OnTransmit(EntityUid messageSource, EntityUid radioSource, int messageLength, bool distorted)
    {
        _transmissionDistorted = distorted;

        if (!_enabled || !TryComp<RadioSfxComponent>(radioSource, out var sfx))
            return;

        if (Transform(radioSource).ParentUid != messageSource)
            return;

        if (!TryComp<ActorComponent>(messageSource, out var actor))
            return;

        var now = _timing.CurTime;
        if (now < sfx.NextSendSound)
            return;

        sfx.NextSendSound = now + sfx.SendCooldown;

        PlayPrivate(sfx.SendStartSound, actor.PlayerSession, sfx.PrivateParams);

        if (sfx.SendEndSound != null)
        {
            var delay = Math.Clamp(sfx.EndDelayBase + messageLength * sfx.EndDelayPerChar, 0.4f, 3f);
            _pending.Add(new PendingEndClick(now + TimeSpan.FromSeconds(delay), radioSource, messageSource));
        }
    }

    private void OnReceive(EntityUid uid, RadioSfxComponent sfx, ref RadioReceiveEvent args)
    {
        if (!_enabled)
            return;

        if (uid == args.RadioSource)
            return;

        var holder = Transform(uid).ParentUid;

        if (holder == args.MessageSource)
            return;

        var now = _timing.CurTime;
        var hasActor = TryComp<ActorComponent>(holder, out var actor);

        if (hasActor && now >= sfx.NextReceiveSound)
        {
            sfx.NextReceiveSound = now + sfx.ReceiveCooldown;

            PlayPrivate(sfx.ReceiveSound, actor!.PlayerSession, sfx.PrivateParams);

            if (_transmissionDistorted)
                PlayPrivate(sfx.StaticSound, actor.PlayerSession, sfx.StaticParams);
        }

        if (sfx.AmbientSound != null && now >= sfx.NextAmbientSound)
        {
            sfx.NextAmbientSound = now + sfx.AmbientCooldown;

            var filter = Filter.Pvs(uid);
            if (hasActor)
                filter.RemovePlayerByAttachedEntity(holder);

            foreach (var session in filter.Recipients)
            {
                var gain = GetGain(session);
                if (gain <= 0.001f)
                    continue;

                _audio.PlayEntity(sfx.AmbientSound, Filter.SinglePlayer(session), uid, false,
                    ApplyGain(sfx.AmbientParams, gain));
            }
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pending.Count == 0)
            return;

        var now = _timing.CurTime;
        for (var i = _pending.Count - 1; i >= 0; i--)
        {
            var pending = _pending[i];
            if (now < pending.At)
                continue;

            _pending.RemoveAt(i);

            if (!TryComp<RadioSfxComponent>(pending.Device, out var sfx)
                || !TryComp<ActorComponent>(pending.Speaker, out var actor))
                continue;

            PlayPrivate(sfx.SendEndSound, actor.PlayerSession, sfx.PrivateParams);
        }
    }

    private void PlayPrivate(SoundSpecifier? sound, ICommonSession session, AudioParams audioParams)
    {
        if (sound == null)
            return;

        var gain = GetGain(session);
        if (gain <= 0.001f)
            return;

        _audio.PlayGlobal(sound, Filter.SinglePlayer(session), false, ApplyGain(audioParams, gain));
    }

    private float GetGain(ICommonSession session)
    {
        return _volume.GetValueOrDefault(session.UserId, 1f);
    }

    private static AudioParams ApplyGain(AudioParams audioParams, float gain)
    {
        return gain >= 0.999f
            ? audioParams
            : audioParams.WithVolume(audioParams.Volume + SharedAudioSystem.GainToVolume(gain));
    }
}