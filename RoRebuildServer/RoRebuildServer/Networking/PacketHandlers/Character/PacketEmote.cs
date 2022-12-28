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
        var map = connection.Character?.Map;

        if (connection.Character == null || connection.Player == null || map == null)
            return;

        var emote = msg.ReadInt32();
        if (emote > 100)
        {
            CommandBuilder.SendRequestFailed(connection.Player!, ClientErrorType.MalformedRequest);
            return;
        }

        var player = connection.Player;
        if (player.InActionCooldown() || player.LastEmoteTime + 1f > Time.DeltaTimeFloat)
            return;
        
        map.GatherPlayersForMultiCast(ref connection.Entity, connection.Character);
        CommandBuilder.SendEmoteMulti(connection.Character, emote);
        CommandBuilder.ClearRecipients();

        player.LastEmoteTime = Time.DeltaTimeFloat;
    }
}