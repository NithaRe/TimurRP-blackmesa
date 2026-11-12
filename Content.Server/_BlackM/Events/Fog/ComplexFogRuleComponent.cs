namespace Content.Server._BlackM.Events.Fog;

[RegisterComponent, Access(typeof(ComplexFogRule))]
public sealed partial class ComplexFogRuleComponent : Component
{
    [DataField]
    public float FadeInTime = 30f;

    [DataField]
    public float FadeOutTime = 40f;

    [DataField]
    public float Density = 0.85f;

    [DataField]
    public Color Color = Color.FromHex("#c3cec6");

    [DataField]
    public bool IncludeMaps = true;

    [ViewVariables]
    public HashSet<EntityUid> AffectedEntities = new();
}
