using System;
using System.Collections.Generic;
using Content.Shared.Actions;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.SecurityCommand;

public sealed partial class OpenSecurityCommandActionEvent : InstantActionEvent
{
}

public sealed partial class OpenSecurityMapActionEvent : InstantActionEvent
{
}

[Serializable, NetSerializable]
public enum SecurityOrderType : byte
{
    Move,
    Post,
    PatrolRoute,
    PatrolZone,
    Danger,
    Label,
}

[Serializable, NetSerializable]
public enum SecurityOrderTargetKind : byte
{
    All,
    Squad,
    Officer,
}

[Serializable, NetSerializable]
public enum SecurityOfficerStatus : byte
{
    Alive,
    Critical,
    Dead,
    Offline,
}

[Serializable, NetSerializable]
public enum SecurityPostDutyStatus : byte
{
    Waiting,
    Serving,
    Completed,
    LeftEarly,
}

[Serializable, NetSerializable]
public sealed class SecurityOrderData
{
    public int Id;
    public SecurityOrderType Type;
    public List<NetCoordinates> Points = new();
    public string Text = string.Empty;
    public Color Color = Color.White;
    public SecurityOrderTargetKind TargetKind;
    public int SquadId;
    public NetEntity? Officer;
    public string IssuerName = string.Empty;
    public TimeSpan IssuedAt;
    public int PostDurationMinutes;
    public SecurityPostDutyStatus PostDutyStatus;
    public TimeSpan PostDutyEndsAt;
}

[Serializable, NetSerializable]
public sealed class SecuritySquadData
{
    public int Id;
    public string Name = string.Empty;
    public Color Color = Color.White;
    public List<NetEntity> Members = new();
}

[Serializable, NetSerializable]
public sealed class SecurityOfficerData
{
    public NetEntity Entity;
    public string Name = string.Empty;
    public string Job = string.Empty;

    public string JobId = string.Empty;

    public int SquadId;
    public SecurityOfficerStatus Status;
    public bool IsCommander;
}

public record struct SecurityOrderDraft(
    SecurityOrderTargetKind TargetKind,
    int SquadId,
    NetEntity? Officer,
    string Text,
    Color Color,
    int PostDurationMinutes = 0);

[Serializable, NetSerializable]
public sealed class SecurityCommandStateMessage : EntityEventArgs
{
    public bool IsCommander;
    public int OwnSquadId;
    public List<SecurityOrderData> Orders = new();
    public List<SecuritySquadData> Squads = new();
    public List<SecurityOfficerData> Officers = new();
}

[Serializable, NetSerializable]
public sealed class SecurityCommandOpenUiMessage : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class SecurityOfficerPosition
{
    public NetEntity Entity;
    public NetCoordinates Coordinates;
}

[Serializable, NetSerializable]
public sealed class SecurityCommandPositionsMessage : EntityEventArgs
{
    public List<SecurityOfficerPosition> Positions = new();
}

[Serializable, NetSerializable]
public sealed class SecurityCommandCreateOrderMessage : EntityEventArgs
{
    public SecurityOrderType Type;
    public List<NetCoordinates> Points = new();
    public string Text = string.Empty;
    public Color Color = Color.White;
    public SecurityOrderTargetKind TargetKind;
    public int SquadId;
    public NetEntity? Officer;
    public int PostDurationMinutes;
}

[Serializable, NetSerializable]
public sealed class SecurityCommandRemoveOrderMessage : EntityEventArgs
{
    public int OrderId;

    public SecurityCommandRemoveOrderMessage(int orderId)
    {
        OrderId = orderId;
    }
}

[Serializable, NetSerializable]
public sealed class SecurityCommandClearOrdersMessage : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class SecurityCommandCreateSquadMessage : EntityEventArgs
{
    public string Name;
    public Color Color;

    public SecurityCommandCreateSquadMessage(string name, Color color)
    {
        Name = name;
        Color = color;
    }
}

[Serializable, NetSerializable]
public sealed class SecurityCommandRemoveSquadMessage : EntityEventArgs
{
    public int SquadId;

    public SecurityCommandRemoveSquadMessage(int squadId)
    {
        SquadId = squadId;
    }
}

[Serializable, NetSerializable]
public sealed class SecurityCommandAssignOfficerMessage : EntityEventArgs
{
    public NetEntity Officer;
    public int SquadId;

    public SecurityCommandAssignOfficerMessage(NetEntity officer, int squadId)
    {
        Officer = officer;
        SquadId = squadId;
    }
}
