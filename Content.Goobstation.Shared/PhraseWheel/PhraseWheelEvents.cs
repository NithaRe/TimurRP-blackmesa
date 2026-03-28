using Robust.Shared.Serialization;

namespace Content.Goobstation.Shared.PhraseWheel;

/// <summary>
/// Клиент отправляет на сервер когда игрок выбрал фразу.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlayPhraseWheelMessage : EntityEventArgs
{
    public string PhraseId { get; init; } = string.Empty;
}
