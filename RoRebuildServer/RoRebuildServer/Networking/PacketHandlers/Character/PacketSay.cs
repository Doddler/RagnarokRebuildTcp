using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.Logging;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Networking.PacketHandlers.Character;

[ClientPacketHandler(PacketType.Say)]
public class PacketSay : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        var map = connection.Character?.Map;
         
        if (!connection.IsConnectedAndInGame)
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

        if (isShout)
        {
            if (connection.Player!.MaxLearnedLevelOfSkill(CharacterSkill.BasicMastery) < 7)
            {
                CommandBuilder.ErrorMessage(connection.Player, $"You need level 7 of basic mastery to use shout chat.");
                return;
            }

            if (connection.Player.ShoutCooldown > Time.ElapsedTimeFloat && !connection.Player.IsAdmin)
            {
                CommandBuilder.ErrorMessage(connection.Player, $"You must wait more time before using shout again.");
                return;
            }
            ServerLogger.Log($"Shout chat message from [{connection.Player!.Name}]: {text}");
            CommandBuilder.AddAllPlayersAsRecipients();
            connection.Player.ShoutCooldown = Time.ElapsedTimeFloat + 20f;
        }
        else
            CommandBuilder.AddRecipients(map.Players); //send to everyone on the map
        //map.AddVisiblePlayersAsPacketRecipients(connection.Character);
        CommandBuilder.SendSayMulti(connection.Character, connection.Character.Name, text, isShout);
        CommandBuilder.ClearRecipients();
    }

}