using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using Lidgren.Network;
using RebuildData.Server.Logging;
using RebuildData.Shared.Data;
using RebuildData.Shared.Enum;
using RebuildData.Shared.Networking;
using RebuildZoneServer.EntityComponents;
using RebuildZoneServer.Util;

namespace RebuildZoneServer.Networking.PacketHandlers
{
	class PacketLookTowards : ClientPacketHandler
	{
		public override PacketType PacketType => PacketType.LookTowards;

		public override void Process(InboundMessage msg)
		{
			if (connection.Character == null)
				return;

			var player = connection.Entity.Get<Player>();
			if (player.InActionCooldown())
			{
				ServerLogger.Debug("Player look action ignored due to cooldown.");
				return;
			}
			player.AddActionDelay(CooldownActionType.FaceDirection);

			var dir = (Direction)msg.ReadByte();
			var head = (HeadFacing)msg.ReadByte();
			connection.Character.ChangeLookDirection(ref connection.Entity, dir, head);
		}
	}
}
