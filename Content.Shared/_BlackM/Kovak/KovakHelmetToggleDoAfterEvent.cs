// SPDX-FileCopyrightText: 2026 BlackM Project
// SPDX-License-Identifier: CC-BY-SA-3.0

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Kovak;

[Serializable, NetSerializable]
public sealed partial class KovakHelmetToggleDoAfterEvent : SimpleDoAfterEvent;
