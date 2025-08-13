using RebuildSharedData.Enum;
using RebuildSharedData.Networking;

namespace RoRebuildServer.Networking.PacketHandlers.Character;

[ClientPacketHandler(PacketType.ChangeName)]
public class PacketChangeName: IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (connection.Character == null || connection.Player == null || connection.Character.Map == null)
            return;

        CommandBuilder.ErrorMessage(connection, "Change name command no longer available.");
        CommandBuilder.SendRequestFailed(connection.Player, ClientErrorType.CommandUnavailable);

        //var str = msg.ReadString();

        //if (str.Length > SharedConfig.MaxPlayerName)
        //{
        //    CommandBuilder.SendRequestFailed(connection.Player, ClientErrorType.RequestTooLong);
        //    return;
        //}

        //connection.Player.Name = str;
        //connection.Character.Name = str;

        //connection.Character.Map.AddVisiblePlayersAsPacketRecipients(connection.Character);
        //CommandBuilder.SendChangeNameMulti(connection.Character, str);
        //CommandBuilder.ClearRecipients();
    }
}