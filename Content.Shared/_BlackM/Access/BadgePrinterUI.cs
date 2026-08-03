using System;
using System.Collections.Generic;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Access;

[Serializable, NetSerializable]
public enum BadgePrinterUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class BadgePrinterBuiState : BoundUserInterfaceState
{
    public bool HasCard;
    public List<BadgePrinterOptionData> Options;

    public BadgePrinterBuiState(bool hasCard, List<BadgePrinterOptionData> options)
    {
        HasCard = hasCard;
        Options = options;
    }
}

[Serializable, NetSerializable]
public sealed class BadgePrinterOptionData
{
    public string ProtoId;
    public string Name;
    public string Description;
    public string IconRsi;
    public string IconState;

    public int? Remaining;

    public BadgePrinterOptionData(string protoId, string name, string description, string iconRsi, string iconState, int? remaining = null)
    {
        ProtoId = protoId;
        Name = name;
        Description = description;
        IconRsi = iconRsi;
        IconState = iconState;
        Remaining = remaining;
    }
}

[Serializable, NetSerializable]
public sealed class BadgePrinterPrintMessage : BoundUserInterfaceMessage
{
    public List<string> SelectedBadgeProtoIds;

    public BadgePrinterPrintMessage(List<string> selectedBadgeProtoIds)
    {
        SelectedBadgeProtoIds = selectedBadgeProtoIds;
    }
}

[Serializable, NetSerializable]
public sealed class BadgePrinterEjectCardMessage : BoundUserInterfaceMessage
{
}
