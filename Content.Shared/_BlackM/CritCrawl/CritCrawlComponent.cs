using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._BlackM.CritCrawl;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CritCrawlComponent : Component
{
    [DataField, AutoNetworkedField]
    public float SpeedModifier = 0.2f;

    [DataField]
    public List<string> WoundGroups = new() { "Brute" };

    [DataField]
    public float MinWoundFraction = 0.5f;

    [DataField]
    public float MaxCritProgress = 0.75f;

    [DataField, AutoNetworkedField]
    public bool WoundCrit;

    #region Trail

    [DataField]
    public float TrailDistance = 0.4f;

    [DataField]
    public float TrailBaseAmount = 0.15f;

    [DataField]
    public float TrailBleedFactor = 0.2f;

    [DataField]
    public float TrailMaxAmount = 1.5f;

    [DataField]
    public float HeavyBleedThreshold = 2.5f;

    [DataField]
    public float HandprintDistance = 1.1f;

    [DataField]
    public float DropsChance = 0.25f;

    [DataField]
    public float PuddleDistance = 3f;

    [DataField]
    public SoundSpecifier? CrawlSound = new SoundCollectionSpecifier("BlackMCritCrawl", AudioParams.Default.WithVolume(-6f).WithVariation(0.15f));

    [DataField]
    public SoundSpecifier? WetCrawlSound = new SoundCollectionSpecifier("BlackMCritCrawlWet", AudioParams.Default.WithVolume(-4f).WithVariation(0.15f));

    [DataField]
    public float CrawlSoundDistance = 0.9f;

    [ViewVariables]
    public float TrailAccumulator;

    [ViewVariables]
    public float HandAccumulator;

    [ViewVariables]
    public float SoundAccumulator;

    [ViewVariables]
    public float PuddleAccumulator;

    [ViewVariables]
    public bool NextHandLeft;

    [ViewVariables]
    public Vector2 TrailDirection = Vector2.Zero;

    #endregion

    [ViewVariables]
    public bool LastCanCrawl;
}
