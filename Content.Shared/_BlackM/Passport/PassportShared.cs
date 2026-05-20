using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Passport;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PassportComponent : Component
{
    [DataField, AutoNetworkedField] public string OwnerName      { get; set; } = string.Empty;
    [DataField, AutoNetworkedField] public string Surname        { get; set; } = string.Empty;
    [DataField, AutoNetworkedField] public string City           { get; set; } = string.Empty;
    [DataField, AutoNetworkedField] public string JobTitle       { get; set; } = string.Empty;
    [DataField, AutoNetworkedField] public string PassportNumber { get; set; } = string.Empty;
    [DataField, AutoNetworkedField] public string IssuedDate     { get; set; } = string.Empty;
    [DataField, AutoNetworkedField] public NetEntity? OwnerEntity { get; set; } = null;
    [DataField, AutoNetworkedField] public bool IsBound          { get; set; } = false;
    [DataField, AutoNetworkedField] public bool HasBureaucraticError { get; set; } = false;
    [DataField, AutoNetworkedField] public string ErrorField { get; set; } = string.Empty;
    [DataField, AutoNetworkedField] public string ErrorValue { get; set; } = string.Empty;
}

[Serializable, NetSerializable]
public sealed class PassportBoundUserInterfaceState : BoundUserInterfaceState
{
    public string OwnerName;
    public string Surname;
    public string City;
    public string JobTitle;
    public string PassportNumber;
    public string IssuedDate;
    public string Signature;
    public string MrzLine1;
    public string MrzLine2;
    public NetEntity? OwnerEntity;
    public bool HasBureaucraticError;
    public string ErrorField;

    public PassportBoundUserInterfaceState(
        string ownerName, string surname, string city, string jobTitle,
        string passportNumber, string issuedDate,
        string signature, string mrzLine1, string mrzLine2, NetEntity? ownerEntity,
        bool hasBureaucraticError, string errorField)
    {
        OwnerName            = ownerName;
        Surname              = surname;
        City                 = city;
        JobTitle             = jobTitle;
        PassportNumber       = passportNumber;
        IssuedDate           = issuedDate;
        Signature            = signature;
        MrzLine1             = mrzLine1;
        MrzLine2             = mrzLine2;
        OwnerEntity          = ownerEntity;
        HasBureaucraticError = hasBureaucraticError;
        ErrorField           = errorField;
    }
}

[Serializable, NetSerializable]
public enum PassportUiKey : byte { Key }