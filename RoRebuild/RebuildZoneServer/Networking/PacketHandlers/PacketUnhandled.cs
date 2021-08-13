using System;
using System.Collections.Generic;
using System.Text;
using Lidgren.Network;
using RebuildData.Server.Logging;
using RebuildData.Shared.Networking;

namespace RebuildZoneServer.Networking.PacketHandlers
{
	class UnhandledPacketHandler : ClientPacketHandler
	{
		public override PacketType PacketType => PacketType.UnhandledPacket;

        public override void Process(InboundMessage msg)
		{
			ServerLogger.LogWarning($"Received unhandled packet type {State.LastPacketType}. Player will be disconnected.");
			State.PacketHandlers[(int) PacketType.Disconnect](msg);
		}
	}
}
