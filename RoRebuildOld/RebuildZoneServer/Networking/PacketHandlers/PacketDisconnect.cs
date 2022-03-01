using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using Lidgren.Network;
using RebuildData.Shared.Networking;

namespace RebuildZoneServer.Networking.PacketHandlers
{
	class PacketDisconnect : ClientPacketHandler
	{
		public override PacketType PacketType => PacketType.Disconnect;

		public override void Process(InboundMessage msg)
		{
			NetworkManager.DisconnectPlayer(connection);
		}
	}
}
