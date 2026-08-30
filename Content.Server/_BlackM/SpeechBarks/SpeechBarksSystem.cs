using Robust.Shared.Prototypes;
using Content.Shared._BlackM.SpeechBarks;
using Content.Shared._BlackM.CCVar;
using Content.Server.Chat.Systems;
using Robust.Shared.Configuration;
using Content.Server.Mind;
using Robust.Shared.Player;
using Content.Server.Examine;
using Content.Shared.Ghost;

namespace Content.Server._BlackM.SpeechBarks;

public sealed class SpeechBarksSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly ExamineSystem _examine = default!;

    private const float HearingRange = 10f;

    private bool _enabled;

    private readonly HashSet<EntityUid> _suppressed = new();

    public override void Initialize()
    {
        base.Initialize();

        _cfg.OnValueChanged(BlackMCVars.BarksEnabled, value => _enabled = value, true);

        SubscribeLocalEvent<SpeechBarksComponent, EntitySpokeEvent>(OnEntitySpoke);
    }

    public void SuppressNextBark(EntityUid uid)
    {
        _suppressed.Add(uid);
    }

    private void OnEntitySpoke(EntityUid uid, SpeechBarksComponent component, EntitySpokeEvent args)
    {
        if (_suppressed.Remove(uid))
            return;

        if (!_enabled)
            return;

        var transformEvent = new TransformSpeakerBarkEvent(uid, component.Data.Clone());
        RaiseLocalEvent(uid, transformEvent);

        var data = transformEvent.Data;
        var sound = data.Sound ?? _proto.Index(data.Proto).Sound;
        var message = args.Message;
        var isWhisper = args.IsWhisper;

        var sourceCoords = Transform(uid).Coordinates;

        foreach (var listener in _lookup.GetEntitiesInRange(sourceCoords, HearingRange))
        {
            if (!_mind.TryGetMind(listener, out _, out var mind))
                continue;

            if (mind.UserId == null || !_player.TryGetSessionById(mind.UserId, out var session))
                continue;

            if (!HasComp<GhostHearingComponent>(listener) && !_examine.InRangeUnOccluded(listener, uid, HearingRange))
                continue;

            RaiseNetworkEvent(new PlaySpeechBarksEvent(
                GetNetEntity(uid),
                message,
                sound,
                data.Pitch,
                data.MinVar,
                data.MaxVar,
                isWhisper), session);
        }
    }
}
