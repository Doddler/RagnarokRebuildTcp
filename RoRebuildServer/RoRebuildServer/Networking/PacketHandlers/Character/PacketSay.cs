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
         
        if (!connection.IsConnectedAndInGame || map == null)
            return;

        var text = msg.ReadString();
        var type = (PlayerChatType)msg.ReadByte();
        //var isShout = msg.ReadBoolean();
        if (text.Length > 140)
        {
            CommandBuilder.SendRequestFailed(connection.Player!, ClientErrorType.RequestTooLong);
            return;
        }

        if (type < 0 || type >= PlayerChatType.Notice)
            return;

#if DEBUG
        if(type == PlayerChatType.Shout)
            ServerLogger.Log($"Shout chat message from [{connection.Player!.Name}]: {text}");
        else
            ServerLogger.Log($"Chat message from [{connection.Player!.Name}]: {text}");
#endif
        var p = connection.Player;

        if (type == PlayerChatType.Party)
        {
            if (p.Party == null)
            {
                CommandBuilder.ErrorMessage(p, "Cannot send chat to party when you aren't in a party.");
                return;
            }

            CommandBuilder.AddRecipients(p.Party.OnlineMembers);
            CommandBuilder.SendSayMulti(connection.Character, p.Character.Name, text, PlayerChatType.Party);
            return;
        }

        if (type == PlayerChatType.Shout)
        {
            if (connection.Player!.MaxLearnedLevelOfSkill(CharacterSkill.BasicMastery) < 7)
            {
                CommandBuilder.ErrorMessage(connection.Player, $"You need level 7 of basic mastery to use shout chat.");
                return;
            }

            if (connection.Player.ShoutCooldown > Time.ElapsedTimeFloat + 20f && !connection.Player.IsAdmin)
            {
                CommandBuilder.ErrorMessage(connection.Player, $"You must wait more time before using shout again.");
                return;
            }
            ServerLogger.Log($"Shout chat message from [{connection.Player!.Name}]: {text}");
            CommandBuilder.AddAllPlayersAsRecipients();
            if (p.ShoutCooldown < Time.ElapsedTime)
                p.ShoutCooldown = Time.ElapsedTimeFloat + 20f;
            else
                p.ShoutCooldown += 20f;
        }
        else
            CommandBuilder.AddRecipients(map.Players); //send to everyone on the map
        //map.AddVisiblePlayersAsPacketRecipients(connection.Character);
        CommandBuilder.SendSayMulti(connection.Character, connection.Character!.Name, text, type);
        CommandBuilder.ClearRecipients();
    }

}