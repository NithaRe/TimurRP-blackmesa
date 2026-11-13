using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.FoodVendor;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FoodVendorComponent : Component
{
    [DataField, AutoNetworkedField]
    public string Group = "food";

    [DataField, AutoNetworkedField]
    public int Limit = 4;

    [DataField]
    public List<ProtoId<JobPrototype>> CookJobs = new() { "ServiceWorker" };

    [DataField]
    public List<string> CookPositions = new() { "ExtraServiceworker" };

    [DataField, AutoNetworkedField]
    public bool LockWhenCookPresent = true;

    [DataField, AutoNetworkedField]
    public bool Locked;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FoodVendorUserComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<string, int> Taken = new();
}

[Serializable, NetSerializable]
public enum FoodVendorVisuals : byte
{
    Locked,
}
