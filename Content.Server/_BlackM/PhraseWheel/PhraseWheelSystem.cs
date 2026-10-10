using Content.Server._BlackM.SpeechBarks;
using Content.Server.Chat.Systems;
using Content.Shared._BlackM.PhraseWheel;
using Content.Shared.Chat;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._BlackM.PhraseWheel;

public enum PhraseWheelAccessResult : byte
{
    Granted,
    Updated,
    Revoked,
}

public sealed class PhraseWheelSystem : SharedPhraseWheelSystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SpeechBarksSystem _barks = default!;

    private static readonly AudioParams SpeakAudio = AudioParams.Default.WithVolume(6f).WithMaxDistance(15f);
    private static readonly AudioParams WhisperAudio = AudioParams.Default.WithVolume(2f).WithMaxDistance(5f);
    private static readonly AudioParams ShoutAudio = AudioParams.Default.WithVolume(8f).WithMaxDistance(25f);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PlayPhraseWheelMessage>(OnPlayPhrase);
    }

    private void OnPlayPhrase(PlayPhraseWheelMessage msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } uid)
            return;

        if (!TryComp<PhraseWheelComponent>(uid, out var comp))
            return;

        if (!_proto.TryIndex<PhraseWheelEntryPrototype>(msg.Phrase.Id, out var phrase))
            return;

        if (!IsCategoryAllowed(comp, phrase.Category))
            return;

        if (!CanUse(uid, comp, PhraseWheelConstants.CooldownTolerance))
            return;

        if (!CanSend(uid, phrase))
            return;

        StartCooldown(comp);

        Color? textColor = phrase.TextColor;
        if (phrase.AllowCustomColor && msg.CustomColor is { } custom)
            textColor = SanitizeColor(custom);

        RaiseNetworkEvent(new PhraseWheelIconEvent
        {
            Source = GetNetEntity(uid),
            Phrase = phrase.ID,
        }, Filter.Pvs(uid));

        _barks.SuppressNextBark(uid);
        _chat.TrySendInGameICMessage(uid, phrase.Text, ToIcChatType(phrase.ChatType), false,
            colorOverride: textColor);

        if (phrase.Sound != null)
            _audio.PlayPvs(phrase.Sound, uid, GetAudioParams(phrase.ChatType));
    }

    private static InGameICChatType ToIcChatType(PhraseWheelChatType type) => type switch
    {
        PhraseWheelChatType.Whisper => InGameICChatType.Whisper,
        PhraseWheelChatType.Emote => InGameICChatType.Emote,
        _ => InGameICChatType.Speak,
    };

    private static AudioParams GetAudioParams(PhraseWheelChatType type) => type switch
    {
        PhraseWheelChatType.Whisper => WhisperAudio,
        PhraseWheelChatType.Shout => ShoutAudio,
        _ => SpeakAudio,
    };

    public bool GrantAccess(EntityUid uid, HashSet<ProtoId<PhraseWheelCategoryPrototype>> categories)
    {
        var existed = HasComp<PhraseWheelComponent>(uid);
        var comp = EnsureComp<PhraseWheelComponent>(uid);
        comp.AllowedCategories = categories;
        Dirty(uid, comp);
        return !existed;
    }

    public bool RevokeAccess(EntityUid uid)
    {
        if (!HasComp<PhraseWheelComponent>(uid))
            return false;

        RemComp<PhraseWheelComponent>(uid);
        return true;
    }

    public PhraseWheelAccessResult UpdateAccess(EntityUid uid,
        HashSet<ProtoId<PhraseWheelCategoryPrototype>> categories)
    {
        if (TryComp<PhraseWheelComponent>(uid, out var existing))
        {
            if (categories.Count == 0)
            {
                RemComp<PhraseWheelComponent>(uid);
                return PhraseWheelAccessResult.Revoked;
            }

            existing.AllowedCategories = categories;
            Dirty(uid, existing);
            return PhraseWheelAccessResult.Updated;
        }

        var comp = EnsureComp<PhraseWheelComponent>(uid);
        comp.AllowedCategories = categories;
        Dirty(uid, comp);
        return PhraseWheelAccessResult.Granted;
    }
}
