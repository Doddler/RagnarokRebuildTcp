using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.Logging;

namespace RoRebuildServer.Networking.PacketHandlers;

[ClientPacketHandler(PacketType.Say)]
public class PacketSay : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (connection.Character == null)
            return;

        var map = connection.Character.Map;
        if (map == null)
            return;

        var text = msg.ReadString();
        if (text.Length > 255)
        {
            CommandBuilder.SendRequestFailed(connection.Player!, ClientErrorType.MalformedRequest);
        }

#if DEBUG
        ServerLogger.Log($"Chat message from '{connection.Player!.Name}: {text}");
#endif

        map.GatherPlayersForMultiCast(ref connection.Entity, connection.Character);
        CommandBuilder.SendSayMulti(connection.Character, text);
        CommandBuilder.ClearRecipients();
    }
}