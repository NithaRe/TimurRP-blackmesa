// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.PhraseWheel;
using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Robust.Server.Audio;
using Robust.Server.Console;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Console;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Client._BlackM.PhraseWheel;

public sealed class PhraseWheelSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IServerConsoleHost _console = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PlayPhraseWheelMessage>(OnPlayPhrase);

        // phrasewheel <ник> [категория1] [категория2] ...
        // phrasewheel <ник>          — выдать/забрать доступ ко всем категориям
        // phrasewheel <ник> HECU     — только раздел HECU
        // phrasewheel <ник> HECU Медики — разделы HECU и Медики
        _console.RegisterCommand("phrasewheel",
            "phrasewheel <ник> [категория...] — выдать/забрать доступ к меню фраз",
            "phrasewheel <ник> [категория...]",
            PhraseWheelCommand);
    }

    private void OnPlayPhrase(PlayPhraseWheelMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;
        if (player == null) return;

        if (!TryComp<PhraseWheelComponent>(player.Value, out var comp)) return;
        if (!_proto.TryIndex<PhraseWheelEntryPrototype>(msg.PhraseId, out var phrase)) return;

        // Проверяем что игрок имеет доступ к этой категории
        if (comp.AllowedCategories.Count > 0 && !comp.AllowedCategories.Contains(phrase.Category))
            return;

        var chatType = phrase.ChatType switch
        {
            PhraseWheelChatType.Whisper => InGameICChatType.Whisper,
            PhraseWheelChatType.Emote   => InGameICChatType.Emote,
            _                           => InGameICChatType.Speak,
        };

        _chat.TrySendInGameICMessage(player.Value, phrase.Text, chatType, false);

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

    private void PhraseWheelCommand(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1)
        {
            shell.WriteError("Использование: phrasewheel <ник> [категория1] [категория2] ...");
            shell.WriteLine("Без категорий — выдаёт/забирает доступ ко всем фразам.");
            shell.WriteLine("С категориями — выдаёт доступ только к указанным разделам.");
            shell.WriteLine("Пример: phrasewheel Vasya HECU Медики");
            return;
        }

        var name = args[0];
        ICommonSession? targetSession = null;

        foreach (var session in _playerManager.Sessions)
        {
            if (session.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                targetSession = session;
                break;
            }
        }

        if (targetSession?.AttachedEntity == null)
        {
            shell.WriteError($"Игрок '{name}' не найден или не в игре.");
            return;
        }

        var uid = targetSession.AttachedEntity.Value;

        // Собираем категории из аргументов (начиная с args[1])
        var categories = args.Skip(1).ToList();

        if (HasComp<PhraseWheelComponent>(uid))
        {
            var existing = Comp<PhraseWheelComponent>(uid);

            // Если категории не указаны — полностью убрать доступ
            if (categories.Count == 0)
            {
                RemComp<PhraseWheelComponent>(uid);
                shell.WriteLine($"Доступ к меню фраз ЗАБРАН у {name}.");
                return;
            }

            // Если категории указаны — обновить список
            existing.AllowedCategories = categories;
            Dirty(uid, existing);
            shell.WriteLine($"Доступ к категориям [{string.Join(", ", categories)}] ОБНОВЛЁН у {name}.");
        }
        else
        {
            var newComp = EnsureComp<PhraseWheelComponent>(uid);
            newComp.AllowedCategories = categories;
            Dirty(uid, newComp);

            if (categories.Count == 0)
                shell.WriteLine($"Доступ ко ВСЕМ фразам ВЫДАН игроку {name}.");
            else
                shell.WriteLine($"Доступ к категориям [{string.Join(", ", categories)}] ВЫДАН игроку {name}.");
        }
    }
}
