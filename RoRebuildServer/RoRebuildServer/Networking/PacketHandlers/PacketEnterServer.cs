using RebuildSharedData.Data;
using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Logging;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Networking.PacketHandlers;


[ClientPacketHandler(PacketType.EnterServer)]
public class PacketEnterServer : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (connection.Character != null)
            return;

        //var playerEntity = NetworkManager.World.CreatePlayer(connection, "prt_fild08", Area.CreateAroundPoint(new Position(170, 367), 5));
        var playerEntity = NetworkManager.World.CreatePlayer(connection, "prontera", Area.CreateAroundPoint(new Position(248, 42), 5));
        connection.Entity = playerEntity;
        connection.LastKeepAlive = Time.ElapsedTime;
        connection.Character = playerEntity.Get<WorldObject>();
        connection.Character.IsActive = false;
        var networkPlayer = playerEntity.Get<Player>();
        networkPlayer.Connection = connection;
        connection.Player = networkPlayer;

        ServerLogger.Debug($"Player assigned entity {playerEntity}, creating entity at location {connection.Character.Position}.");

        CommandBuilder.InformEnterServer(connection.Character, networkPlayer);
    }
}