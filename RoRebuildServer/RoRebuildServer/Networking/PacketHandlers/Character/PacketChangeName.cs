using RebuildSharedData.Networking;
using RoRebuildServer.Database;
using RoRebuildServer.Database.Requests;
using RebuildSharedData.Util;

namespace RoRebuildServer.Networking.PacketHandlers.Character;

[AdminClientPacketHandler(PacketType.ChangeName)]
public class PacketChangeName : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (connection.Character == null || connection.Player == null || connection.Character.Map == null)
            return;

        //CommandBuilder.ErrorMessage(connection, "Change name command no longer available.");
        //CommandBuilder.SendRequestFailed(connection.Player, ClientErrorType.CommandUnavailable);

        var str = msg.ReadString();

        var nameError = NameValidator.GetCharacterNameError(str);
        if (nameError != null)
        {
            CommandBuilder.ErrorMessage(connection, nameError);
            return;
        }

        if (connection.ActiveDbAction != ActiveDbAction.None)
        {
            CommandBuilder.ErrorMessage(connection, "Unable to perform the requested action, please try again.");
            return;
        }

        //the request checks for duplicates and saves; Player's update applies it
        var request = new ChangeNameRequest(connection, connection.Player.Id, str);
        connection.ChangeNameRequest = request;
        connection.ActiveDbAction = ActiveDbAction.ChangeName;
        RoDatabase.EnqueueDbRequest(request);
    }
}