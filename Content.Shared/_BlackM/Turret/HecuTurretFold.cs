using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Turret;

[RegisterComponent]
public sealed partial class HecuTurretFoldedComponent : Component
{
    [DataField]
    public EntProtoId TurretPrototype = "BlackMHecuLaserTurret";

    [DataField]
    public TimeSpan DeployTime = TimeSpan.FromSeconds(2.5);

    [DataField]
    public SoundSpecifier? DeploySound = new SoundPathSpecifier("/Audio/Items/ratchet.ogg");
}

[Serializable, NetSerializable]
public sealed partial class HecuTurretFoldDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public sealed partial class HecuTurretDeployDoAfterEvent : DoAfterEvent
{
    [DataField]
    public NetCoordinates Location;

    private HecuTurretDeployDoAfterEvent()
    {
    }

    public HecuTurretDeployDoAfterEvent(NetCoordinates location)
    {
        Location = location;
    }

    public override DoAfterEvent Clone() => this;
}
