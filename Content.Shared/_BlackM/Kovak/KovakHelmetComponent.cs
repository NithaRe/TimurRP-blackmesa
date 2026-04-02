// SPDX-FileCopyrightText: 2026 BlackM Project
// SPDX-License-Identifier: CC-BY-SA-3.0

using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._BlackM.Kovak;

/// <summary>
/// Компонент шлема КОВАК с переключением забрала,
/// звуком и сообщением в чат.
/// </summary>
[RegisterComponent]
public sealed partial class KovakHelmetComponent : Component
{
    /// <summary>
    /// Открыто ли забрало сейчас.
    /// </summary>
    [DataField]
    public bool IsOpen = false;

    /// <summary>
    /// Префикс экипировки когда забрало закрыто.
    /// </summary>
    [DataField]
    public string? ClosedEquippedPrefix = null;

    /// <summary>
    /// Префикс экипировки когда забрало открыто.
    /// </summary>
    [DataField]
    public string OpenEquippedPrefix = "open";

    /// <summary>
    /// Иконка когда забрало закрыто.
    /// </summary>
    [DataField]
    public string ClosedIconState = "icon";

    /// <summary>
    /// Иконка когда забрало открыто.
    /// </summary>
    [DataField]
    public string OpenIconState = "open-icon";

    /// <summary>
    /// Текст в чате когда забрало закрывается.
    /// </summary>
    [DataField]
    public string CloseMessage = "[КОВАК] Забрало опущено.";

    /// <summary>
    /// Текст в чате когда забрало открывается.
    /// </summary>
    [DataField]
    public string OpenMessage = "[КОВАК] Забрало поднято.";

    /// <summary>
    /// Цвет сообщения в чате (hex).
    /// </summary>
    [DataField]
    public string MessageColor = "#7B2FBE";

    /// <summary>
    /// Имя говорящего в чате (над головой игрока).
    /// </summary>
    [DataField]
    public string SpeakerName = "Шлем КОВАК";

    /// <summary>
    /// Звук закрытия забрала.
    /// </summary>
    [DataField]
    public SoundSpecifier? CloseSound = new SoundPathSpecifier("/Audio/_BlackM/Kovak/helmet_close.ogg");

    /// <summary>
    /// Звук открытия забрала.
    /// </summary>
    [DataField]
    public SoundSpecifier? OpenSound = new SoundPathSpecifier("/Audio/_BlackM/Kovak/helmet_open.ogg");

    /// <summary>
    /// Время переключения забрала (секунды).
    /// </summary>
    [DataField]
    public float ToggleDelay = 2.5f;
}