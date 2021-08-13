using System;
using System.Collections.Generic;
using System.Text;
using Lidgren.Network;
using RebuildData.Server.Logging;
using RebuildData.Shared.Networking;

namespace RebuildZoneServer.Networking.PacketHandlers
{
	public class PacketPlayerReady : ClientPacketHandler
	{
		public override PacketType PacketType => PacketType.PlayerReady;

        public override void Process(InboundMessage msg)
		{
			if (connection.Character == null)
				return;

			connection.Character.IsActive = true;
			connection.Character.Map.SendAllEntitiesToPlayer(ref connection.Entity);

			connection.Character.Map.SendAddEntityAroundCharacter(ref connection.Entity, connection.Character);

			connection.Character.SpawnImmunity = 5f;

			ServerLogger.Debug($"Player {connection.Entity} finished loading, spawning him on {connection.Character.Map.Name} at position {connection.Character.Position}.");
		}
	}
}
