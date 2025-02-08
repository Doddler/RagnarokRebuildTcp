using System.Diagnostics;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.Data;
using RoRebuildServer.Logging;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Networking.PacketHandlers.Character;

[ClientPacketHandler(PacketType.Emote)]
public class PacketEmote : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (!connection.IsConnectedAndInGame)
            return;

        Debug.Assert(connection.Player != null);
        Debug.Assert(connection.Character != null);
        Debug.Assert(connection.Character.Map != null);

        if (connection.Player.JobId == 0 && connection.Player.MaxLearnedLevelOfSkill(CharacterSkill.BasicMastery) < 1)
        {
            CommandBuilder.ErrorMessage(connection.Player, $"You need level 1 of basic mastery to use emotes.");
            return;
        }

        var emote = msg.ReadInt32();
        if (emote > 100)
        {
            CommandBuilder.SendRequestFailed(connection.Player, ClientErrorType.MalformedRequest);
            return;
        }

        var player = connection.Player;
        if (player.InActionCooldown() || player.LastEmoteTime + 1.8f > Time.ElapsedTimeFloat)
            return;

        if (emote >= 58 && emote <= 63)
            emote = 200 + GameRandom.NextInclusive(0, 5); //200 is out of the valid range, client will map this onto the right dice action
        else
        {
            if (!DataManager.ValidEmotes.Contains(emote))
            {
                ServerLogger.LogWarning($"User {player} trying to use emote {emote} but it doesn't appear to be a valid emote.");
                return;
            }
        }
        
        connection.Character.Map.AddVisiblePlayersAsPacketRecipients(connection.Character);
        CommandBuilder.SendEmoteMulti(connection.Character, emote);
        CommandBuilder.ClearRecipients();

        player.LastEmoteTime = Time.ElapsedTimeFloat;
    }
}