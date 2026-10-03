using Robust.Shared.GameStates;

namespace Content.Shared._BlackM.NameTag;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NameTagComponent : Component
{
    [DataField, AutoNetworkedField]
    public string Text = string.Empty;

    [DataField, AutoNetworkedField]
    public Color Color = Color.White;

    [DataField, AutoNetworkedField]
    public bool Rainbow = true;

    [DataField, AutoNetworkedField]
    public NameTagAuraStyle AuraStyle = NameTagAuraStyle.None;
}