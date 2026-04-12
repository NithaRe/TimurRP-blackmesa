// Originally authored by mirrorcult for EphemeralSpace (https://github.com/EphemeralSpace/ephemeral-space)
// Ported to this project with modifications under the same license.

using JetBrains.Annotations;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;

namespace Content.Shared._BlackM.Viewcone;

/// <summary>
///     API for spawning viewcone effects and making sure source gets set correctly +
///     it spawns in the correct pos and shit
/// </summary>
[PublicAPI]
public sealed class BMViewconeEffectSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    /// <summary>
    ///     Spawns the given effect entity at the player source, and sets relevant variables
    /// </summary>
    /// <param name="source">The player that originated the effect, or the entity to spawn next to if a relevant player doesn't exist</param>
    /// <param name="effect">The prototype ID of an effect entity to spawn</param>
    /// <param name="angleOverride">The local rotation to set the effect to, instead of the parent rotation.</param>
    public void SpawnEffect(EntityUid source, EntProtoId effect, Angle? angleOverride = null)
    {
        if (_net.IsClient)
            return;

        var ent = SpawnNextToOrDrop(effect, source);
        var viewconeEffect = EnsureComp<BMViewconeOccludableComponent>(ent);
        viewconeEffect.Inverted = true;
        viewconeEffect.Source = source;
        Dirty(ent, viewconeEffect);

        _xform.SetLocalRotation(ent, angleOverride ?? Transform(source).LocalRotation);

        EnsureComp<TimedDespawnComponent>(ent);
    }
}
