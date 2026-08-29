using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Audio;

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

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class BarkData
{
    [DataField]
    public ProtoId<BlackMBarkPrototype> Proto = "DefaultBark";

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier? Sound = null;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MinVar = 0.1f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxVar = 0.5f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Pitch = 1f;

    public BarkData()
    {
    }

    public BarkData(ProtoId<BlackMBarkPrototype> proto, float pitch, float minVar, float maxVar)
    {
        Proto = proto;
        Pitch = pitch;
        MinVar = minVar;
        MaxVar = maxVar;
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

    public BarkData WithMinVar(float value)
    {
        var clone = Clone();
        clone.MinVar = value;
        return clone;
    }

    public BarkData WithMaxVar(float value)
    {
        var clone = Clone();
        clone.MaxVar = value;
        return clone;
    }

    public BarkData Clone()
    {
        return new BarkData
        {
            Proto = Proto,
            Sound = Sound,
            Pitch = Pitch,
            MinVar = MinVar,
            MaxVar = MaxVar,
        };
    }

    public bool Equals(BarkData other)
    {
        return Proto == other.Proto
            && Sound == other.Sound
            && Pitch.Equals(other.Pitch)
            && MinVar.Equals(other.MinVar)
            && MaxVar.Equals(other.MaxVar);
    }
}
