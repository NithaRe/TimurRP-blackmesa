using System;

namespace Content.Shared._BlackM.PhraseWheel;

public static class PhraseWheelConstants
{
    public static readonly TimeSpan UseCooldown = TimeSpan.FromSeconds(3);

    public static readonly TimeSpan CooldownTolerance = TimeSpan.FromMilliseconds(300);

    public const float MinTextLuminance = 0.25f;
}
