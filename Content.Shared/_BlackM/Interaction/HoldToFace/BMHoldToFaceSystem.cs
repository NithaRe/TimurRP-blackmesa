// Originally authored by mirrorcult for EphemeralSpace (https://github.com/EphemeralSpace/ephemeral-space)
// Ported to this project with modifications under the same license.

using Content.Shared.CombatMode;
using Content.Shared.Input;
using Content.Shared.MouseRotator;
using Content.Shared.Movement.Components;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;

namespace Content.Shared._BlackM.Interaction.HoldToFace;

public sealed class BMHoldToFaceSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.BMHoldToFace,
                InputCmdHandler.FromDelegate(args => ToggleRotator(args, true), args => ToggleRotator(args, false), false, false))
            .Register<BMHoldToFaceSystem>();
    }

    private void ToggleRotator(ICommonSession? session, bool value)
    {
        if (session?.AttachedEntity is not { } ent || !HasComp<BMHoldToFaceComponent>(ent))
            return;

        // Don't try and override combat mode doing the same thing
        if (TryComp<CombatModeComponent>(ent, out var combat) && combat is { ToggleMouseRotator: true, IsInCombatMode: true })
            return;

        if (value)
        {
            EnsureComp<MouseRotatorComponent>(ent);
            EnsureComp<NoRotateOnMoveComponent>(ent);
        }
        else
        {
            RemComp<MouseRotatorComponent>(ent);
            RemComp<NoRotateOnMoveComponent>(ent);
        }
    }
}