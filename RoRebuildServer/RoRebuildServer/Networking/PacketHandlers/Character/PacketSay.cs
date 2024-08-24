using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.Logging;

namespace RoRebuildServer.Networking.PacketHandlers.Character;

[ClientPacketHandler(PacketType.Say)]
public class PacketSay : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        var map = connection.Character?.Map;
         
        if (connection.Character == null || connection.Player == null || map == null)
            return;

        var text = msg.ReadString();
        var isShout = msg.ReadBoolean();
        if (text.Length > 140)
        {
            CommandBuilder.SendRequestFailed(connection.Player!, ClientErrorType.RequestTooLong);
            return;
        }

#if DEBUG
        if(isShout)
            ServerLogger.Log($"Shout chat message from [{connection.Player!.Name}]: {text}");
        else
            ServerLogger.Log($"Chat message from [{connection.Player!.Name}]: {text}");
#endif

        if(isShout)
            CommandBuilder.AddAllPlayersAsRecipients();
        else
            CommandBuilder.AddRecipients(map.Players); //send to everyone on the map
        //map.AddVisiblePlayersAsPacketRecipients(connection.Character);
        CommandBuilder.SendSayMulti(connection.Character, connection.Character.Name, text, isShout);
        CommandBuilder.ClearRecipients();
    }

}