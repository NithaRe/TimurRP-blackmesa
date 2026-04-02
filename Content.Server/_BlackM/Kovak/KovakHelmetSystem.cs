// SPDX-FileCopyrightText: 2026 BlackM Project
// SPDX-License-Identifier: CC-BY-SA-3.0

using Content.Server.Chat.Systems;
using Content.Shared._BlackM.Kovak;
using Content.Shared.Chat;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.DoAfter;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server._BlackM.Kovak;

public sealed class KovakHelmetSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ClothingSystem _clothing = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<KovakHelmetToggleMessage>(OnToggleAttempt);
        SubscribeLocalEvent<KovakHelmetComponent, KovakHelmetToggleDoAfterEvent>(OnToggleComplete);
    }

    private void OnToggleAttempt(KovakHelmetToggleMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;
        if (player == null)
            return;

        var helmet = GetEntity(msg.Helmet);

        if (!TryComp<KovakHelmetComponent>(helmet, out var comp))
            return;

        // Запускаем DoAfter с полосой прогресса
        _doAfter.TryStartDoAfter(new DoAfterArgs(
            EntityManager,
            player.Value,
            comp.ToggleDelay,
            new KovakHelmetToggleDoAfterEvent(),
            helmet)
        {
            NeedHand = false,
            BreakOnMove = true,
            BreakOnDamage = true,
        });
    }

    private void OnToggleComplete(EntityUid uid, KovakHelmetComponent comp, KovakHelmetToggleDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        var player = args.User;

        // Переключаем состояние
        comp.IsOpen = !comp.IsOpen;

        // Обновляем спрайт на персонаже
        _clothing.SetEquippedPrefix(uid, comp.IsOpen
            ? comp.OpenEquippedPrefix
            : comp.ClosedEquippedPrefix);

        // Воспроизводим звук
        var sound = comp.IsOpen ? comp.OpenSound : comp.CloseSound;
        if (sound != null)
            _audio.PlayPvs(sound, uid);

        // Шлем говорит от своего имени
        var message = comp.IsOpen ? comp.OpenMessage : comp.CloseMessage;
        _chat.TrySendInGameICMessage(
            player,
            message,
            InGameICChatType.Speak,
            hideChat: false,
            checkRadioPrefix: false,
            nameOverride: comp.SpeakerName,
            colorOverride: System.Drawing.Color.FromArgb(
                int.Parse(comp.MessageColor.TrimStart('#').Substring(0, 2), System.Globalization.NumberStyles.HexNumber),
                int.Parse(comp.MessageColor.TrimStart('#').Substring(2, 2), System.Globalization.NumberStyles.HexNumber),
                int.Parse(comp.MessageColor.TrimStart('#').Substring(4, 2), System.Globalization.NumberStyles.HexNumber)
            )
        );
    }
}