using Robust.Shared.Utility;

namespace Content.Server._BlackM.Map;

[RegisterComponent]
public sealed partial class DepartmentSpawnerComponent : Component
{
    [DataField]
    public List<ResPath> Variants = new();

    // Persist this so saving a generated station cannot generate the department again.
    [DataField]
    public bool Spawned;
}

[RegisterComponent]
public sealed partial class DepartmentOriginComponent : Component;
