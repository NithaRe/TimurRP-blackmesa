using Robust.Shared.ViewVariables;

namespace Content.Server._BlackM.Ams;

[RegisterComponent]
public sealed partial class AmsControlledDoorComponent : Component
{
    [DataField(required: true), ViewVariables(VVAccess.ReadWrite)]
    public string GroupId = string.Empty;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string AnnouncementLocationName = string.Empty;
}
