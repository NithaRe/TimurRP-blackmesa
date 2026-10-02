using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.HireTerminal;

[Serializable, NetSerializable]
public enum HireTerminalUiKey : byte { Key }

[Serializable, NetSerializable]
public enum HireTerminalMode : byte
{
    SelfService,

    NeedsReview
}

[Serializable, NetSerializable]
public enum HirePassportStatus : byte
{
    NoPassport,

    Invalid,

    CanApply,

    Pending,

    ReadyToClaim,

    Rejected,

    AlreadyHas
}

[Serializable, NetSerializable]
public sealed class HirePositionInfo
{
    public string Id;
    public string Name;
    public string Department;
    public int DepartmentOrder;
    public string Description;
    public Color Color;

    public HirePositionInfo(string id, string name, string department, int departmentOrder, string description, Color color)
    {
        Id = id;
        Name = name;
        Department = department;
        DepartmentOrder = departmentOrder;
        Description = description;
        Color = color;
    }
}

[Serializable, NetSerializable]
public sealed class HirePendingInfo
{
    public int Id;
    public string ApplicantName;
    public string CurrentJob;
    public string PositionName;
    public string Department;
    public string Comment;
    public string Time;

    public HirePendingInfo(int id, string applicantName, string currentJob, string positionName,
        string department, string comment, string time)
    {
        Id = id;
        ApplicantName = applicantName;
        CurrentJob = currentJob;
        PositionName = positionName;
        Department = department;
        Comment = comment;
        Time = time;
    }
}

[Serializable, NetSerializable]
public sealed class HireLogEntry
{
    public string Time;
    public string Name;
    public string PositionName;
    public string Department;
    public string ApprovedBy;
    public Color Color;

    public HireLogEntry(string time, string name, string positionName, string department, string approvedBy, Color color)
    {
        Time = time;
        Name = name;
        PositionName = positionName;
        Department = department;
        ApprovedBy = approvedBy;
        Color = color;
    }
}

[Serializable, NetSerializable]
public sealed class HireTerminalBoundUserInterfaceState : BoundUserInterfaceState
{
    public HireTerminalMode Mode;
    public HirePassportStatus Status;

    public string HolderName;
    public string HolderJob;
    public string PassportNumber;
    public string AdditionalJob;

    public string StatusNote;

    public string ApplicationPosition;

    public List<HirePositionInfo> Positions;
    public List<HirePendingInfo> Pending;
    public List<HireLogEntry> Log;

    public List<NetEntity> Reviewers;

    public HireTerminalBoundUserInterfaceState(
        HireTerminalMode mode, HirePassportStatus status,
        string holderName, string holderJob, string passportNumber, string additionalJob,
        string statusNote, string applicationPosition,
        List<HirePositionInfo> positions, List<HirePendingInfo> pending,
        List<HireLogEntry> log, List<NetEntity> reviewers)
    {
        Mode = mode;
        Status = status;
        HolderName = holderName;
        HolderJob = holderJob;
        PassportNumber = passportNumber;
        AdditionalJob = additionalJob;
        StatusNote = statusNote;
        ApplicationPosition = applicationPosition;
        Positions = positions;
        Pending = pending;
        Log = log;
        Reviewers = reviewers;
    }
}

[Serializable, NetSerializable]
public sealed class HireTerminalSubmitMessage : BoundUserInterfaceMessage
{
    public string PositionId;
    public string Comment;

    public HireTerminalSubmitMessage(string positionId, string comment)
    {
        PositionId = positionId;
        Comment = comment;
    }
}

[Serializable, NetSerializable]
public sealed class HireTerminalClaimMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class HireTerminalEjectMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class HireTerminalApproveMessage : BoundUserInterfaceMessage
{
    public int ApplicationId;
    public HireTerminalApproveMessage(int id) => ApplicationId = id;
}

[Serializable, NetSerializable]
public sealed class HireTerminalRejectMessage : BoundUserInterfaceMessage
{
    public int ApplicationId;
    public HireTerminalRejectMessage(int id) => ApplicationId = id;
}

[Serializable, NetSerializable]
public enum HireTerminalVisuals : byte { HasPassport }

[Serializable, NetSerializable]
public enum HireTerminalVisualLayers : byte { Base, Screen, Passport }
