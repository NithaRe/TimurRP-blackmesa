using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._BlackM.Audio;

/// <summary>
/// Хранит желаемое и текущее состояние EFX эффекта для аудио источника.
/// </summary>
[RegisterComponent]
public sealed partial class BlackMAudioEffectStateComponent : Component
{
    /// <summary>Базовый эффект (реверб помещения).</summary>
    [ViewVariables]
    public ProtoId<AudioPresetPrototype>? BasePreset;

    /// <summary>
    /// Временный эффект с более высоким приоритетом (например заглушение).
    /// Перекрывает BasePreset пока активен.
    /// </summary>
    [ViewVariables]
    public ProtoId<AudioPresetPrototype>? OverridePreset;

    /// <summary>Пресет который сейчас реально назначен на источник.</summary>
    [ViewVariables]
    public ProtoId<AudioPresetPrototype>? AppliedPreset;

    /// <summary>Нужно ли синхронизировать желаемое и применённое состояние.</summary>
    [ViewVariables]
    public bool NeedsReconcile = true;
}
