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
    public string? HolderName;
    public string? HolderJob;
    public List<BadgePrinterOptionData> Options;

    public BadgePrinterBuiState(bool hasCard, string? holderName, string? holderJob, List<BadgePrinterOptionData> options)
    {
        HasCard = hasCard;
        HolderName = holderName;
        HolderJob = holderJob;
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
    public string Reason;

    public BadgePrinterPrintMessage(List<string> selectedBadgeProtoIds, string reason)
    {
        SelectedBadgeProtoIds = selectedBadgeProtoIds;
        Reason = reason;
    }
}

[Serializable, NetSerializable]
public sealed class BadgePrinterEjectCardMessage : BoundUserInterfaceMessage
{
}
