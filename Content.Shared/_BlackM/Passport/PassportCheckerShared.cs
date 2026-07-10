using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Passport;

[Serializable, NetSerializable]
public enum PassportCheckerUiKey : byte { Key }

[Serializable, NetSerializable]
public sealed class PassportCheckerBoundUserInterfaceState : BoundUserInterfaceState
{
    public bool HasPassport;

    public string? RefOwnerName;
    public string? RefSurname;
    public string? RefCity;
    public string? RefJobTitle;
    public string? RefPassportNumber;
    public string? RefIssuedDate;
    public string? DocOwnerName;
    public string? DocSurname;
    public string? DocCity;
    public string? DocJobTitle;
    public string? DocPassportNumber;
    public string? DocIssuedDate;
    public NetEntity? OwnerEntity;
    public string? SelectedField;
    public PassportCheckerResult Result;
    public string? ConfirmedErrorField;

    public PassportCheckerBoundUserInterfaceState(
        bool hasPassport,
        string? refOwnerName, string? refSurname, string? refCity, string? refJobTitle,
        string? refPassportNumber, string? refIssuedDate,
        string? docOwnerName, string? docSurname, string? docCity, string? docJobTitle,
        string? docPassportNumber, string? docIssuedDate,
        NetEntity? ownerEntity,
        string? selectedField,
        PassportCheckerResult result,
        string? confirmedErrorField)
    {
        HasPassport = hasPassport;

        RefOwnerName      = refOwnerName;
        RefSurname        = refSurname;
        RefCity           = refCity;
        RefJobTitle       = refJobTitle;
        RefPassportNumber = refPassportNumber;
        RefIssuedDate     = refIssuedDate;

        DocOwnerName      = docOwnerName;
        DocSurname        = docSurname;
        DocCity           = docCity;
        DocJobTitle       = docJobTitle;
        DocPassportNumber = docPassportNumber;
        DocIssuedDate     = docIssuedDate;

        OwnerEntity         = ownerEntity;
        SelectedField       = selectedField;
        Result              = result;
        ConfirmedErrorField = confirmedErrorField;
    }
}

public enum PassportCheckerResult : byte
{
    None,
    CorrectCatch,
    WrongAccusation,
    CorrectClean,
    Missed
}

[Serializable, NetSerializable]
public sealed class PassportCheckerSelectFieldMessage : BoundUserInterfaceMessage
{
    public string Field;

    public PassportCheckerSelectFieldMessage(string field) => Field = field;
}

[Serializable, NetSerializable]
public sealed class PassportCheckerAccuseMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class PassportCheckerConfirmCleanMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class PassportCheckerEjectMessage : BoundUserInterfaceMessage { }
