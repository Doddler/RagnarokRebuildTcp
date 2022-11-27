using RebuildSharedData.Config;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;

namespace RoRebuildServer.Networking.PacketHandlers;

[ClientPacketHandler(PacketType.ChangeName)]
public class PacketChangeName: IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (connection.Character == null || connection.Player == null || connection.Character.Map == null)
            return;

        var str = msg.ReadString();

        if (str.Length > SharedConfig.MaxPlayerName)
        {
            CommandBuilder.SendRequestFailed(connection.Player, ClientErrorType.RequestTooLong);
        }

        connection.Player.Name = str;

        connection.Character.Map.GatherPlayersForMultiCast(ref connection.Entity, connection.Character);
        CommandBuilder.SendChangeNameMulti(connection.Character, str);
        CommandBuilder.ClearRecipients();
    }
}