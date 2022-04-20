using RebuildSharedData.Data;
using RebuildSharedData.Networking;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Logging;
using RoRebuildServer.Simulation;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Networking.PacketHandlers;

[ClientPacketHandler(PacketType.AdminEnterServerSpecificMap)]
public class PacketAdminEnterServerSpecificMap : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (!ServerConfig.DebugConfig.EnableEnterSpecificMap)
        {
            ServerLogger.LogWarning("Player connected with debug packet EnterServerSpecificMap, disconnecting player.");
            NetworkManager.DisconnectPlayer(connection);
            return;
        }

        if (connection.Character != null)
            return;

        var mapName = msg.ReadString();
        var hasPosition = msg.ReadBoolean();
        var area = Area.Zero;

        if (hasPosition)
        {
            var x = msg.ReadInt16();
            var y = msg.ReadInt16();

            var target = new Position(x, y);
            ServerLogger.Debug($"Player chose to spawn at specific point: {x},{y}");

            area = Area.CreateAroundPoint(target, 0);
        }

        var playerEntity = World.Instance.CreatePlayer(connection, mapName, area);
        connection.Entity = playerEntity;
        connection.LastKeepAlive = Time.ElapsedTime;
        connection.Character = playerEntity.Get<WorldObject>();
        connection.Character.ClassId = 200; //Gamemaster
        connection.Character.MoveSpeed = 0.08f;
        connection.Character.IsActive = false;
        var networkPlayer = playerEntity.Get<Player>();
        networkPlayer.Connection = connection;

        var player = playerEntity.Get<Player>();

        for(var i = 0; i < 99; i++)
            player.LevelUp();

        ServerLogger.Log($"Player assigned entity {playerEntity}, creating entity at location {connection.Character.Position}.");

        CommandBuilder.InformEnterServer(connection.Character, networkPlayer);
    }
}