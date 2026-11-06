using Content.Server.Chat.Systems;
using Content.Shared._BlackM.CCVar;
using Content.Shared._BlackM.SpeechBarks;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.SpeechBarks;

public sealed class SpeechBarksSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private bool _enabled;

    private readonly Dictionary<EntityUid, GameTick> _suppressed = new();

    public override void Initialize()
    {
        base.Initialize();

        _cfg.OnValueChanged(BlackMCVars.BarksEnabled, OnEnabledChanged, true);

        SubscribeLocalEvent<SpeechBarksComponent, EntitySpokeEvent>(OnEntitySpoke);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _cfg.UnsubValueChanged(BlackMCVars.BarksEnabled, OnEnabledChanged);
    }

    private void OnEnabledChanged(bool value)
    {
        _enabled = value;
    }

    public void SuppressNextBark(EntityUid uid)
    {
        _suppressed[uid] = _timing.CurTick;
    }

    private bool ConsumeSuppression(EntityUid uid)
    {
        return _suppressed.Remove(uid, out var tick) && tick == _timing.CurTick;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_suppressed.Count > 0)
            _suppressed.Clear();
    }

    private void OnEntitySpoke(EntityUid uid, SpeechBarksComponent component, EntitySpokeEvent args)
    {
        if (ConsumeSuppression(uid))
            return;

        if (!_enabled || !args.Language.SpeechOverride.RequireSpeech || args.BarkMessageId == null)
            return;

        var transformEvent = new TransformSpeakerBarkEvent(uid, component.Data.Clone());
        RaiseLocalEvent(uid, transformEvent);

        var data = transformEvent.Data;
        if (!TryGetSound(data, out var sound))
            return;

        RaiseNetworkEvent(new PlaySpeechBarksEvent(
                GetNetEntity(uid),
                args.BarkMessageId.Value,
                sound,
                data.Pitch,
                Math.Clamp(data.Variation, BarkData.MinVariation, BarkData.MaxVariation),
                Math.Clamp(data.Speed, BarkData.MinSpeed, BarkData.MaxSpeed),
                data.Mode,
                args.IsWhisper),
            Filter.Pvs(uid));
    }

    private bool TryGetSound(BarkData data, out SoundSpecifier sound)
    {
        if (data.Sound != null)
        {
            sound = data.Sound;
            return true;
        }

        if (_proto.TryIndex(data.Proto, out var proto) ||
            _proto.TryIndex<BlackMBarkPrototype>(BarkData.DefaultProto, out proto))
        {
            sound = proto.Sound;
            return true;
        }

        sound = default!;
        return false;
    }
}
