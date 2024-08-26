using RebuildSharedData.Networking;
using RoRebuildServer.Data;
using RoRebuildServer.Simulation;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Networking.PacketHandlers.Character;

[ClientPacketHandler(PacketType.ClientTextCommand)]
public class PacketClientTextCommand : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (!connection.IsConnectedAndInGame || connection.Character == null || connection.Player == null)
            return;

        connection.Player.AddActionDelay(0.8f);


        var type = (ClientTextCommand)msg.ReadByte();

        if(type == ClientTextCommand.Adminify)
        {
            var text = msg.ReadString();
            var serverPass = ServerConfig.OperationConfig.AdminifyPasscode;
            if (!string.IsNullOrWhiteSpace(serverPass) && text != serverPass)
            {
                CommandBuilder.ErrorMessage(connection.Player, $"Invalid parameters.");
                return;
            }
            connection.Player.IsAdmin = true; //lol

            CommandBuilder.AddRecipient(connection.Entity);
            CommandBuilder.SendServerMessage($"You are now an admin! Have fun!");
            CommandBuilder.ClearRecipients();
        }

        if (type == ClientTextCommand.Where)
        {
            CommandBuilder.AddRecipient(connection.Entity);
            CommandBuilder.SendServerMessage($"You are at {connection.Character.Position} on map {connection.Character.Map.Name}.");
            CommandBuilder.ClearRecipients();
        }

        if (type == ClientTextCommand.Info)
        {
            var text = $"There are {NetworkManager.PlayerCount} players online and the server has been online for {TimeSpan.FromSeconds(Time.ElapsedTime):c}.";

            CommandBuilder.AddRecipient(connection.Entity);
            CommandBuilder.SendServerMessage(text);
            CommandBuilder.ClearRecipients();
        }
    }
}