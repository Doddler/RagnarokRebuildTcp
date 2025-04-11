using RebuildSharedData.Networking;
using System.Diagnostics;
using RebuildSharedData.Enum;
using RoRebuildServer.Logging;

namespace RoRebuildServer.Networking.PacketHandlers.Party;

[ClientPacketHandler(PacketType.UpdateParty)]
public class PacketUpdateParty : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (!connection.IsConnectedAndInGame)
            return;

        Debug.Assert(connection.Player != null);
        Debug.Assert(connection.Character != null);
        Debug.Assert(connection.Character.Map != null);

        var type = (PartyClientAction)msg.ReadByte();
        var p = connection.Player;
        if (p.Party == null)
            return;

        switch (type)
        {
            case PartyClientAction.LeaveParty:
                p.Party.RemoveMember(p);
                break;
            case PartyClientAction.RemovePlayer:
                if (p.Party.PartyOwnerId != p.PartyMemberId)
                {
                    CommandBuilder.ErrorMessage(p, "You must be party leader to remove someone from party.");
                    return;
                }
                p.Party.RemoveMember(msg.ReadInt32());

                break;
            case PartyClientAction.ChangeLeader:
                if (p.Party.PartyOwnerId != p.PartyMemberId)
                {
                    CommandBuilder.ErrorMessage(p, "You must be party leader to assign someone else as leader.");
                    return;
                }
                p.Party.PromoteMemberToLeader(msg.ReadInt32());
                break;
            case PartyClientAction.DisbandParty:
                if (p.Party.PartyOwnerId != p.PartyMemberId)
                {
                    CommandBuilder.ErrorMessage(p, "You must be party leader to disband the party.");
                    return;
                }
                p.Party.Disband();
                break;
            default:
                ServerLogger.LogWarning($"Player {connection.Player} attempted to perform party action {type}, but it is not implemented.");
                break;
        }
    }
}