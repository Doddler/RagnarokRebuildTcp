using RebuildSharedData.Networking;
using RoRebuildServer.Simulation;

namespace RoRebuildServer.Networking.PacketHandlers.Character;

[ClientPacketHandler(PacketType.ClientTextCommand)]
public class PacketClientTextCommand : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        var type = (ClientTextCommand)msg.ReadByte();

        if (type == ClientTextCommand.Where)
        {
            if (connection.IsConnectedAndInGame && connection.Character != null)
            {
                CommandBuilder.AddRecipient(connection.Entity);
                CommandBuilder.SendServerMessage($"You are at {connection.Character.Position} on map {connection.Character.Map.Name}.");
                CommandBuilder.ClearRecipients();
            }

        }
    }
}