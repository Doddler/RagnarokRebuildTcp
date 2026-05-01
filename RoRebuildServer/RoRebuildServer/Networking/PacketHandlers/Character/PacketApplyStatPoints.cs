using RebuildSharedData.Networking;

namespace RoRebuildServer.Networking.PacketHandlers.Character;

[ClientPacketHandler(PacketType.ApplyStatPoints)]
public class PacketApplyStatPoints : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (!connection.IsConnectedAndInGame || connection.Player == null)
            return;

        var player = connection.Player;

        Span<int> stats = stackalloc int[6];
        for (var i = 0; i < 6; i++)
            stats[i] = msg.ReadInt32();

        player.AddStatPoints(stats);
    }
}