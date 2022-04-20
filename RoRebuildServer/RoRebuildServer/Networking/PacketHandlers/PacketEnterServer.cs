using RebuildSharedData.Data;
using RebuildSharedData.Networking;
using RoRebuildServer.Data;
using RoRebuildServer.Data.Config;
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

        var config = ServerConfig.GetConfigSection<ServerEntryConfig>();

        var map = config.Map;
        var area = Area.CreateAroundPoint(config.Position, config.Area);

        if (connection.LoadCharacterRequest != null)
        {
            var req = connection.LoadCharacterRequest;
            if (req != null && !string.IsNullOrEmpty(req.Map))
            {
                map = req.Map;
                area = Area.CreateAroundPoint(req.Position, 0);
            }
        }

        var playerEntity = NetworkManager.World.CreatePlayer(connection, map, area);
        //var playerEntity = NetworkManager.World.CreatePlayer(connection, "prt_fild08", Area.CreateAroundPoint(new Position(170, 367), 5));
        //var playerEntity = NetworkManager.World.CreatePlayer(connection, "prontera", Area.CreateAroundPoint(new Position(248, 42), 5));
        connection.Entity = playerEntity;
        connection.LastKeepAlive = Time.ElapsedTime;
        connection.Character = playerEntity.Get<WorldObject>();
        connection.Character.IsActive = false;
        var networkPlayer = playerEntity.Get<Player>();
        networkPlayer.Connection = connection;
        connection.Player = networkPlayer;

        ServerLogger.Debug($"Player assigned entity {playerEntity}, creating entity at location {connection.Character.Position}.");

        //CommandBuilder.InformEnterServer(connection.Character, networkPlayer);
    }
}