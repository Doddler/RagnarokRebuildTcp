using RebuildSharedData.Networking;
using System.Diagnostics;
using RoRebuildServer.Database.Requests;
using RoRebuildServer.Database;

namespace RoRebuildServer.Networking.PacketHandlers.Party;

[ClientPacketHandler(PacketType.CreateParty)]
public class PacketCreateParty : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (!connection.IsConnectedAndInGame)
            return;

        Debug.Assert(connection.Player != null);
        Debug.Assert(connection.Character != null);
        Debug.Assert(connection.Character.Map != null);

        if (connection.Player.Party != null)
        {
            CommandBuilder.ErrorMessage(connection, "Unable to organize a new party while already in a party.");
            return;
        }

        var partyName = msg.ReadString();

        if (string.IsNullOrWhiteSpace(partyName))
        {
            CommandBuilder.ErrorMessage(connection, "Party name cannot be empty.");
            return;
        }

        if (partyName.Length > 32)
        {
            CommandBuilder.ErrorMessage(connection, "Party name cannot be longer than 32 characters long.");
            return;
        }
        
        var partyRequest = new CreatePartyRequest(connection, partyName);
        connection.CreatePartyRequest = partyRequest;
        RoDatabase.EnqueueDbRequest(partyRequest);
    }
}