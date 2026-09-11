using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.DiscordAuth;

/// <summary>
/// Client sends this to skip verification when it is optional (discord.auth_is_optional == true).
/// </summary>
public sealed class MsgDiscordAuthByPass : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
    }
}
