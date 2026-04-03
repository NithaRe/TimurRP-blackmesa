using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server._BlackM.BattleMusic;

[RegisterComponent]
public sealed partial class BattleMusicComponent : Component
{
    [DataField]
    public EntityUid? Opponent;

    [DataField]
    public TimeSpan LastHitTime;

    [DataField]
    public float TimeoutSeconds = 20f;

    [DataField]
    public Dictionary<EntityUid, TimeSpan> PendingRetaliation = new();
}
