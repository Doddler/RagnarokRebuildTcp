using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using Lidgren.Network;
using RebuildData.Server.Logging;
using RebuildData.Shared.Networking;

namespace RebuildZoneServer.Networking
{
	public abstract class ClientPacketHandler
	{
		public ServerState State { get; set; }
        public virtual PacketType PacketType => throw new Exception("Packet type not specified on client packet handler.");
        protected NetworkConnection connection;

		public void HandlePacket(InboundMessage msg)
        {
            connection = msg.Client;
            if (msg.Client.Socket.State != WebSocketState.Open)
            {
                ServerLogger.Log("Ignoring message from non open web socket.");
                return;
            }
        

            Process(msg);
		}

        public void HandlePacketNoCheck(InboundMessage msg)
        {
            connection = msg.Client;
            Process(msg);
        }

		public virtual void Process(InboundMessage msg)
		{
			throw new NotImplementedException(); //must be overridden
		}
	}
}
