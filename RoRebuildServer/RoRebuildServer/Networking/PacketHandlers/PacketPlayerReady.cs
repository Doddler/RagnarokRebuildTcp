using RebuildSharedData.Networking;
using RoRebuildServer.Logging;

namespace RoRebuildServer.Networking.PacketHandlers;

[ClientPacketHandler(PacketType.PlayerReady)]
public class PacketPlayerReady : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (connection.Character == null || connection.Character.Map == null)
            return;

        connection.Character.IsActive = true;
        connection.Character.Map.SendAllEntitiesToPlayer(ref connection.Entity);

        connection.Character.Map.SendAddEntityAroundCharacter(ref connection.Entity, connection.Character);

        connection.Character.SpawnImmunity = 5f;

        ServerLogger.Debug($"Player {connection.Entity} finished loading, spawning him on {connection.Character.Map.Name} at position {connection.Character.Position}.");
    }
}