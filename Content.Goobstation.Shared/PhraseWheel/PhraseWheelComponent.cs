// SPDX-License-Identifier: AGPL-3.0-or-later
using Robust.Shared.GameStates;

namespace Content.Goobstation.Shared.PhraseWheel;

/// <summary>
/// Добавляется игроку когда ему выдали доступ к меню фраз.
/// AllowedCategories — список разделов которые видит этот игрок.
/// Пустой список = видит все разделы.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class PhraseWheelComponent : Component
{
    /// <summary>
    /// Разделы к которым есть доступ. Пусто = все разделы.
    /// Заполняется командой phrasewheel.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<string> AllowedCategories { get; set; } = new();
}