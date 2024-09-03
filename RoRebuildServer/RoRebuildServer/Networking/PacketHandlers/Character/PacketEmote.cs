using System.Diagnostics;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
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

        var emote = msg.ReadInt32();
        if (emote > 100)
        {
            CommandBuilder.SendRequestFailed(connection.Player, ClientErrorType.MalformedRequest);
            return;
        }

        var player = connection.Player;
        if (player.InActionCooldown() || player.LastEmoteTime + 1f > Time.ElapsedTimeFloat)
            return;
        
        connection.Character.Map.AddVisiblePlayersAsPacketRecipients(connection.Character);
        CommandBuilder.SendEmoteMulti(connection.Character, emote);
        CommandBuilder.ClearRecipients();

        player.LastEmoteTime = Time.DeltaTimeFloat;
    }
}