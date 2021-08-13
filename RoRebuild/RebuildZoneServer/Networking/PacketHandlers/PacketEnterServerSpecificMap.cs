using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using Lidgren.Network;
using RebuildData.Server.Logging;
using RebuildData.Shared.Data;
using RebuildData.Shared.Networking;
using RebuildZoneServer.Data.Management;
using RebuildZoneServer.EntityComponents;
using RebuildZoneServer.Util;

namespace RebuildZoneServer.Networking.PacketHandlers
{
	class PacketEnterServerSpecificMap : ClientPacketHandler
	{
		public override PacketType PacketType => PacketType.EnterServerSpecificMap;
		public override void Process(InboundMessage msg)
		{
			if (!State.DebugMode)
			{
				ServerLogger.LogWarning("Player connected with debug packet EnterServerSpecificMap, disconnecting player.");
				State.PacketHandlers[(int)PacketType.Disconnect](msg); //yeah no
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

			var playerEntity = State.World.CreatePlayer(connection, mapName, area);
			connection.Entity = playerEntity;
			connection.LastKeepAlive = Time.ElapsedTime;
			connection.Character = playerEntity.Get<Character>();
			connection.Character.ClassId = 200; //Gamemaster
			connection.Character.MoveSpeed = 0.08f;
			connection.Character.IsActive = false;
			var networkPlayer = playerEntity.Get<Player>();
			networkPlayer.Connection = connection;

			var ce = connection.Entity.Get<CombatEntity>();
			ce.Stats.Atk = 9999;
			ce.Stats.Atk2 = 9999;

			ServerLogger.Log($"Player assigned entity {playerEntity}, creating entity at location {connection.Character.Position}.");

			CommandBuilder.InformEnterServer(connection.Character, networkPlayer);
		}
	}
}
