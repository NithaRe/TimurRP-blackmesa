// SPDX-License-Identifier: AGPL-3.0-or-later
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
namespace Content.Shared._BlackM.CascadeResonance;

[RegisterComponent]
public sealed partial class CascadeResonanceComponent : Component
{
    // Роли которые могут активировать
    [DataField]
    public List<string> AllowedJobs { get; set; } = new() { "BmesaResearchDirector" };

    // Длительность таймера в секундах
    [DataField]
    public float Duration { get; set; } = 120f;

    // Уже запущен?
    [DataField]
    public bool Active { get; set; } = false;

    // Сколько времени прошло
    [DataField]
    public float Elapsed { get; set; } = 0f;

    // Текст оповещений
    [DataField]
    public string StartMessage { get; set; } = "cascade-resonance-start";

    [DataField]
    public string CountdownMessage { get; set; } = "cascade-resonance-countdown";

    [DataField]
    public string CompleteMessage { get; set; } = "cascade-resonance-complete";

    // Звуки
    [DataField]
    public SoundSpecifier? StartSound { get; set; }

    [DataField]
    public SoundSpecifier? CountdownSound { get; set; }

    [DataField]
    public SoundSpecifier? CompleteSound { get; set; }

    // Точка телепорта — ID entity на карте
    [DataField]
    public string TeleportTargetTag { get; set; } = "HecuTeleportTarget";

    [DataField]
    public EntityUid DeviceUid { get; set; }
}