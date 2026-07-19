using Content.Server.Chat.Systems;
using Content.Shared._BlackM.Megaphone;
using Content.Shared.Chat;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Timing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Localization;
using Robust.Shared.Maths;

namespace Content.Server._BlackM.Megaphone;

public sealed class MegaphoneSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;
    [Dependency] private readonly ILocalizationManager _loc = default!;

    private static readonly Color MegaphoneColor = Color.FromHex("#F1C40F");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MegaphoneComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<MegaphoneComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<MegaphoneComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, MegaphoneComponent comp, MapInitEvent args)
    {
        EnsureComp<UseDelayComponent>(uid);
    }

    private void OnUseInHand(EntityUid uid, MegaphoneComponent comp, UseInHandEvent args)
    {
        if (args.Handled) return;
        args.Handled = TryShout(uid, comp, args.User);
    }

    private void OnActivate(EntityUid uid, MegaphoneComponent comp, ActivateInWorldEvent args)
    {
        if (args.Handled) return;
        args.Handled = TryShout(uid, comp, args.User);
    }

    private bool TryShout(EntityUid uid, MegaphoneComponent comp, EntityUid user)
    {
        if (!TryComp<UseDelayComponent>(uid, out var delay))
            return false;

        if (_useDelay.IsDelayed((uid, delay)))
            return false;

        _useDelay.TryResetDelay((uid, delay));

        _audio.PlayPvs(comp.Sound, uid);

        var phrase = _loc.GetString(comp.PhraseLocKey);
        var speakerName = _loc.GetString(comp.SpeakerNameLocKey);

        _chat.TrySendInGameICMessage(
            user,
            phrase,
            InGameICChatType.Speak,
            hideChat: false,
            checkRadioPrefix: false,
            nameOverride: speakerName,
            colorOverride: MegaphoneColor
        );

        return true;
    }
}
