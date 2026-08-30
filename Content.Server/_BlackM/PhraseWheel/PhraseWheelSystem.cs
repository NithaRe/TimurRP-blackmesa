using Content.Shared._BlackM.PhraseWheel;
using Content.Server.Chat.Systems;
using Content.Server._BlackM.SpeechBarks;
using Content.Shared.Chat;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Console;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Collections.Generic;

namespace Content.Server._BlackM.PhraseWheel;

public sealed class PhraseWheelSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SpeechBarksSystem _barks = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _lastUse = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PlayPhraseWheelMessage>(OnPlayPhrase);
        SubscribeLocalEvent<PhraseWheelComponent, ComponentShutdown>(OnCompShutdown);
    }

    private void OnCompShutdown(EntityUid uid, PhraseWheelComponent comp, ComponentShutdown args)
    {
        _lastUse.Remove(uid);
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

        if (_lastUse.TryGetValue(player.Value, out var lastUse)
            && _timing.CurTime - lastUse < PhraseWheelConstants.UseCooldown)
            return;
        _lastUse[player.Value] = _timing.CurTime;

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

        if (phrase.Icon is SpriteSpecifier.Texture tex)
        {
            RaiseNetworkEvent(new PhraseWheelIconEvent
            {
                Source = GetNetEntity(player.Value),
                IconPath = tex.TexturePath.ToString(),
            }, Filter.Pvs(player.Value));
        }

        Timer.Spawn(100, () =>
        {
            if (!Exists(player.Value)) return;

            _barks.SuppressNextBark(player.Value);
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
        });
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
