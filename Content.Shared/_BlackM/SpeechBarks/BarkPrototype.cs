using Content.Shared._BlackM.CCVar;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.SpeechBarks;

[Prototype("speechBark")]
public sealed partial class BlackMBarkPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public bool RoundStart = true;

    [DataField]
    public string Name = "Default";

    [DataField]
    public string Category = "Standard_barks";

    [DataField(required: true)]
    public SoundSpecifier Sound { get; private set; } = default!;
}

[Serializable, NetSerializable]
public enum BarkMode : byte
{
    Letters,
    Syllables,
    Words,
}

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class BarkData
{
    public const string DefaultProto = "DefaultBark";
    public const float DefaultPitch = 1f;
    public const float DefaultVariation = 0.1f;
    public const float DefaultSpeed = 1f;
    public const BarkMode DefaultMode = BarkMode.Syllables;

    public const float MinVariation = 0f;
    public const float MaxVariation = 0.5f;
    public const float MinSpeed = 0.5f;
    public const float MaxSpeed = 2f;

    [DataField]
    public ProtoId<BlackMBarkPrototype> Proto = DefaultProto;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier? Sound;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Pitch = DefaultPitch;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Variation = DefaultVariation;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Speed = DefaultSpeed;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public BarkMode Mode = DefaultMode;

    public BarkData()
    {
    }

    public BarkData(ProtoId<BlackMBarkPrototype> proto, float pitch, float variation, float speed, BarkMode mode)
    {
        Proto = proto;
        Pitch = pitch;
        Variation = variation;
        Speed = speed;
        Mode = mode;
    }

    public BarkData WithProto(string proto)
    {
        var clone = Clone();
        clone.Proto = proto;
        return clone;
    }

    public BarkData WithPitch(float pitch)
    {
        var clone = Clone();
        clone.Pitch = pitch;
        return clone;
    }

    public BarkData WithVariation(float variation)
    {
        var clone = Clone();
        clone.Variation = variation;
        return clone;
    }

    public BarkData WithSpeed(float speed)
    {
        var clone = Clone();
        clone.Speed = speed;
        return clone;
    }

    public BarkData WithMode(BarkMode mode)
    {
        var clone = Clone();
        clone.Mode = mode;
        return clone;
    }

    public BarkData Clone()
    {
        return new BarkData
        {
            Proto = Proto,
            Sound = Sound,
            Pitch = Pitch,
            Variation = Variation,
            Speed = Speed,
            Mode = Mode,
        };
    }

    public bool Equals(BarkData other)
    {
        return Proto == other.Proto
            && Equals(Sound, other.Sound)
            && Pitch.Equals(other.Pitch)
            && Variation.Equals(other.Variation)
            && Speed.Equals(other.Speed)
            && Mode == other.Mode;
    }

    public static BarkData Sanitize(BarkData data, IPrototypeManager proto, IConfigurationManager cfg)
    {
        var result = data.Clone();
        result.Sound = null;

        if (!proto.TryIndex(result.Proto, out var bark) || !bark.RoundStart)
            result.Proto = DefaultProto;

        var minPitch = cfg.GetCVar(BlackMCVars.BarksMinPitch);
        var maxPitch = Math.Max(minPitch, cfg.GetCVar(BlackMCVars.BarksMaxPitch));

        result.Pitch = Fix(result.Pitch, DefaultPitch, minPitch, maxPitch);
        result.Variation = Fix(result.Variation, DefaultVariation, MinVariation, MaxVariation);
        result.Speed = Fix(result.Speed, DefaultSpeed, MinSpeed, MaxSpeed);

        if (result.Mode > BarkMode.Words)
            result.Mode = DefaultMode;

        return result;
    }

    private static float Fix(float value, float fallback, float min, float max)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            value = fallback;

        return Math.Clamp(value, min, max);
    }
}
