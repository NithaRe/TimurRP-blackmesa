// SPDX-FileCopyrightText: 2024 Ed <96445749+TheShuEd@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._BlackM.Environment; // BlackM - NoSpace

namespace Content.Shared.Random.Rules;

/// <summary>
/// Returns true if the attached entity is in space.
/// </summary>
public sealed partial class InSpaceRule : RulesRule
{
    public override bool Check(EntityManager entManager, EntityUid uid)
    {
        if (!entManager.TryGetComponent(uid, out TransformComponent? xform) ||
            xform.GridUid != null)
        {
            return Inverted;
        }

        // BlackM - NoSpace
        if (xform.MapUid is { } map
            && entManager.TryGetComponent(map, out NoSpaceComponent? noSpace)
            && noSpace.NoSpaceAmbience)
        {
            return Inverted;
        }
        // BlackM - NoSpace end

        return !Inverted;
    }
}