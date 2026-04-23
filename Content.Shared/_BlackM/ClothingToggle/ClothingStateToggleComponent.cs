// SPDX-FileCopyrightText: 2026 BlackM Project
// SPDX-License-Identifier: CC-BY-SA-3.0

using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._BlackM.ClothingToggle;

[RegisterComponent]
public sealed partial class ClothingStateToggleComponent : Component
{
    [DataField]
    public bool IsOpen = false;

    [DataField]
    public string? ClosedPrefix = null;

    [DataField]
    public string OpenPrefix = "open";

    [DataField]
    public SoundSpecifier? ToggleSound;

    [DataField]
    public EntProtoId Action = "ActionClothingStateToggle";

    [DataField]
    public EntityUid? ActionEntity;

    [DataField]
    public float ToggleCooldown = 1.5f;

    [DataField]
    public TimeSpan LastToggleTime = TimeSpan.Zero;
}
