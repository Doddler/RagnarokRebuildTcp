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
        if (text.Length > 255)
        {
            CommandBuilder.SendRequestFailed(connection.Player!, ClientErrorType.MalformedRequest);
            return;
        }

#if DEBUG
        ServerLogger.Log($"Chat message from '{connection.Player!.Name}: {text}");
#endif

        map.AddVisiblePlayersAsPacketRecipients(connection.Character);
        CommandBuilder.SendSayMulti(connection.Character, text);
        CommandBuilder.ClearRecipients();
    }

}