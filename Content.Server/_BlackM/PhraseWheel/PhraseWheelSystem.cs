using Content.Shared._BlackM.PhraseWheel;
using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Console;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using System.Collections.Generic;

namespace Content.Server._BlackM.PhraseWheel;

public sealed class PhraseWheelSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PlayPhraseWheelMessage>(OnPlayPhrase);
    }

    private void OnPlayPhrase(PlayPhraseWheelMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;
        if (player == null) return;

        if (!TryComp<PhraseWheelComponent>(player.Value, out var comp)) return;
        if (!_proto.TryIndex<PhraseWheelEntryPrototype>(msg.PhraseId, out var phrase)) return;

        if (TryComp<MobStateComponent>(player.Value, out var mobState))
        {
            if (mobState.CurrentState == MobState.Critical ||
                mobState.CurrentState == MobState.Dead)
                return;
        }

        if (comp.AllowedCategories.Count > 0 && !comp.AllowedCategories.Contains(phrase.Category))
            return;

        Color? colorOverride = null;
        var colorHex = msg.CustomColor ?? phrase.TextColor;
        if (!string.IsNullOrWhiteSpace(colorHex))
        {
            try { colorOverride = Color.FromHex(colorHex); }
            catch { colorOverride = null; }
        }

        var chatType = phrase.ChatType switch
        {
            PhraseWheelChatType.Whisper => InGameICChatType.Whisper,
            PhraseWheelChatType.Emote   => InGameICChatType.Emote,
            _                           => InGameICChatType.Speak,
        };

        _chat.TrySendInGameICMessage(player.Value, phrase.Text, chatType, false,
            colorOverride: colorOverride);

        if (phrase.Sound != null)
        {
            try
            {
                _audio.PlayPvs(phrase.Sound, player.Value,
                    AudioParams.Default.WithVolume(6f).WithMaxDistance(15f));
            }
            catch { }
        }
    }

    public void UpdateAccess(EntityUid uid, List<string> categories, string name, IConsoleShell shell)
    {
        if (HasComp<PhraseWheelComponent>(uid))
        {
            var existing = Comp<PhraseWheelComponent>(uid);

            if (categories.Count == 0)
            {
                RemComp<PhraseWheelComponent>(uid);
                shell.WriteLine($"zabral dostup {name}.");
                return;
            }

            existing.AllowedCategories = categories;
            Dirty(uid, existing);
            shell.WriteLine($"dostup category [{string.Join(", ", categories)}] update y {name}.");
        }
        else
        {
            var newComp = EnsureComp<PhraseWheelComponent>(uid);
            newComp.AllowedCategories = categories;
            Dirty(uid, newComp);

            if (categories.Count == 0)
                shell.WriteLine($"vidan dostyp all phrase {name}.");
            else
                shell.WriteLine($"dostup category [{string.Join(", ", categories)}] give {name}.");
        }
    }
}