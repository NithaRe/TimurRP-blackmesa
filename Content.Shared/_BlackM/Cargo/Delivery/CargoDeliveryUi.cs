using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Cargo.Delivery;

[Serializable, NetSerializable]
public enum CargoDeliveryTerminalUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum CargoDeliveryPadStatus : byte
{
    NoPad,
    NoCrate,
    CrateOpen,
    CrateEmpty,
    CrateHasMob,
    CrateHasForbidden,
    CrateReserved,
    Ready,
}

[Serializable, NetSerializable]
public enum CargoDeliveryDroneVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum CargoDeliveryDroneVisualState : byte
{
    Flying,
    Carrying,
    Downed,
}

[Serializable, NetSerializable]
public enum CargoDeliveryDroneVisualLayers : byte
{
    Base,
}

[Serializable, NetSerializable]
public enum CargoDeliveryPointVisuals : byte
{
    Incoming,
}

[Serializable, NetSerializable]
public enum CargoDeliveryPointVisualLayers : byte
{
    Base,
}

[Serializable, NetSerializable]
public sealed class CargoDeliveryPointEntry
{
    public NetEntity Point;
    public string ProtoId;
    public string Name;
    public string DepartmentName;
    public Color Color;
    public string Location;
    public int Distance;
    public bool Available;
    public int CooldownSeconds;
    public int Incoming;

    public CargoDeliveryPointEntry(NetEntity point, string protoId, string name, string departmentName, Color color,
        string location, int distance, bool available, int cooldownSeconds, int incoming)
    {
        Point = point;
        ProtoId = protoId;
        Name = name;
        DepartmentName = departmentName;
        Color = color;
        Location = location;
        Distance = distance;
        Available = available;
        CooldownSeconds = cooldownSeconds;
        Incoming = incoming;
    }
}

[Serializable, NetSerializable]
public sealed class CargoDeliveryDroneEntry
{
    public NetEntity Drone;
    public string ProtoId;
    public string Name;
    public CargoDeliveryDroneState State;
    public bool HasCrate;
    public bool Own;
    public string Destination;
    public int Distance;
    public int Seconds;
    public int Integrity;

    public CargoDeliveryDroneEntry(NetEntity drone, string protoId, string name, CargoDeliveryDroneState state,
        bool hasCrate, bool own, string destination, int distance, int seconds, int integrity)
    {
        Drone = drone;
        ProtoId = protoId;
        Name = name;
        State = state;
        HasCrate = hasCrate;
        Own = own;
        Destination = destination;
        Distance = distance;
        Seconds = seconds;
        Integrity = integrity;
    }
}

[Serializable, NetSerializable]
public sealed class CargoDeliveryContentEntry
{
    public string ProtoId;
    public string Name;
    public int Count;

    public CargoDeliveryContentEntry(string protoId, string name, int count)
    {
        ProtoId = protoId;
        Name = name;
        Count = count;
    }
}

[Serializable, NetSerializable]
public sealed class CargoDeliveryTerminalState : BoundUserInterfaceState
{
    public List<CargoDeliveryPointEntry> Points;
    public CargoDeliveryPadStatus PadStatus;
    public string? CrateProto;
    public List<CargoDeliveryContentEntry> Contents;
    public List<CargoDeliveryDroneEntry> Drones;
    public int IdleDrones;
    public int Balance;
    public int Reward;
    public int Penalty;
    public int CratePrice;
    public int CratePrintCooldown;
    public int Delivered;
    public int Failed;

    public CargoDeliveryTerminalState(List<CargoDeliveryPointEntry> points, CargoDeliveryPadStatus padStatus,
        string? crateProto, List<CargoDeliveryContentEntry> contents, List<CargoDeliveryDroneEntry> drones,
        int idleDrones, int balance, int reward, int penalty, int cratePrice, int cratePrintCooldown,
        int delivered, int failed)
    {
        Points = points;
        PadStatus = padStatus;
        CrateProto = crateProto;
        Contents = contents;
        Drones = drones;
        IdleDrones = idleDrones;
        Balance = balance;
        Reward = reward;
        Penalty = penalty;
        CratePrice = cratePrice;
        CratePrintCooldown = cratePrintCooldown;
        Delivered = delivered;
        Failed = failed;
    }
}

[Serializable, NetSerializable]
public sealed class CargoDeliverySendMessage : BoundUserInterfaceMessage
{
    public NetEntity Point;

    public CargoDeliverySendMessage(NetEntity point)
    {
        Point = point;
    }
}

[Serializable, NetSerializable]
public sealed class CargoDeliveryPrintCrateMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CargoDeliveryRecallMessage : BoundUserInterfaceMessage
{
    public NetEntity Drone;

    public CargoDeliveryRecallMessage(NetEntity drone)
    {
        Drone = drone;
    }
}
