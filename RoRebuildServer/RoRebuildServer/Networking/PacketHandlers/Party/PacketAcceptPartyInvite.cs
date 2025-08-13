using RebuildSharedData.Networking;
using System.Diagnostics;
using RoRebuildServer.Simulation;

namespace RoRebuildServer.Networking.PacketHandlers.Party;

[ClientPacketHandler(PacketType.AcceptPartyInvite)]
public class PacketAcceptPartyInvite : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (!connection.IsConnectedAndInGame)
            return;

        Debug.Assert(connection.Player != null);
        Debug.Assert(connection.Character != null);
        Debug.Assert(connection.Character.Map != null);

        var p = connection.Player;
        if (p.Party != null)
            return;

        var partyId = msg.ReadInt32();
        if (!World.Instance.TryFindPartyById(partyId, out var party))
        {
            CommandBuilder.ErrorMessage(p, "Unable to join party.");
            return;
        }

        if (!party.HasInvite(p))
        {
            CommandBuilder.ErrorMessage(p, "Party invite request invalid or expired.");
            return;
        }

        p.Party = party;
        p.PartyMemberId = party.PartyId;

        party.AddMember(p);
    }
}