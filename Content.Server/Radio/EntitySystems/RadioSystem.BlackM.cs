using System.Text;
using Content.Server._BlackM.Radio;
using Content.Shared._BlackM.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Random;

namespace Content.Server.Radio.EntitySystems;

public sealed partial class RadioSystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly RadioSfxSystem _radioSfx = default!;

    private const string StaticChars = "#%&*@~";

    private bool TryDistortMessage(string message, out string result)
    {
        result = message;

        var chance = _cfg.GetCVar(BlackMCVars.RadioDistortionChance);
        if (chance <= 0f || message.Length < 4 || !_random.Prob(chance))
            return false;

        var strength = _random.NextFloat(0.08f, 0.25f);
        var sb = new StringBuilder(message.Length);

        foreach (var c in message)
        {
            if (char.IsLetterOrDigit(c) && _random.Prob(strength))
                sb.Append(StaticChars[_random.Next(StaticChars.Length)]);
            else
                sb.Append(c);
        }

        if (_random.Prob(0.25f) && sb.Length > 8)
        {
            var cut = (int) (sb.Length * _random.NextFloat(0.6f, 0.9f));
            if (char.IsLowSurrogate(sb[cut]))
                cut--;

            sb.Length = cut;
            sb.Append("-");
        }

        var garbled = sb.ToString();
        if (garbled == message)
            return false;

        result = garbled;
        return true;
    }
}
