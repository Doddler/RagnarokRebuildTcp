using System;
using System.Collections.Generic;
using System.Text;
using Lidgren.Network;
using RebuildData.Server.Logging;
using RebuildData.Shared.Networking;
using RebuildZoneServer.Util;

namespace RebuildZoneServer.Networking.PacketHandlers
{
	class PacketPing : ClientPacketHandler
	{
		public override PacketType PacketType => PacketType.Ping;

		public override void Process(InboundMessage msg)
		{
			if (connection.Character == null || !connection.Character.IsActive)
			{
				ServerLogger.Debug("Ignored player ping packet as the player isn't alive in the world yet.");
				return; //we don't accept the keep-alive packet if they haven't entered the world yet
			}

			connection.LastKeepAlive = Time.ElapsedTime;
		}
	}
}
