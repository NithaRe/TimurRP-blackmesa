using Robust.Shared.GameStates;

namespace Content.Shared._BlackM.WalkBob;

/// <summary>
/// Заставляет спрайт сущности слегка "дышать" (сжиматься/растягиваться)
/// во время движения, имитируя классический 2D squash-and-stretch эффект ходьбы.
/// Чисто визуальный компонент, обрабатывается только на клиенте.
/// </summary>
[RegisterComponent]
public sealed partial class WalkBobComponent : Component
{
    /// <summary>
    /// Текущая фаза синусоиды качания. Не задавайте вручную, управляется системой.
    /// </summary>
    [DataField]
    public float Phase;

    /// <summary>
    /// Базовая частота цикла качания (умножается на скорость движения).
    /// </summary>
    [DataField]
    public float Frequency = 8f;

    /// <summary>
    /// Амплитуда сжатия/растяжения. Рекомендуется держать в пределах 0.03-0.12,
    /// иначе эффект будет выглядеть как желе, а не как ходьба.
    /// </summary>
    [DataField]
    public float Amplitude = 0.08f;

    /// <summary>
    /// Скорость движения, ниже которой эффект считается "покоем" и сбрасывается.
    /// </summary>
    [DataField]
    public float MinSpeedThreshold = 0.05f;

    /// <summary>
    /// Скорость возврата масштаба в состояние покоя (Vector2.One) при остановке.
    /// </summary>
    [DataField]
    public float ReturnLerpSpeed = 10f;

    /// <summary>
    /// Опциональный ключ слоя спрайта. Если null — качается весь SpriteComponent.
    /// Используйте, если хотите качать только тело, не трогая одежду/предметы в руках.
    /// </summary>
    [DataField]
    public string? TargetLayerKey;
}