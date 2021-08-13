using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using Lidgren.Network;
using RebuildData.Shared.Networking;
using RebuildZoneServer.Config;
using RebuildZoneServer.Sim;

namespace RebuildZoneServer.Networking
{
	public class ServerState
	{
		public NetServer Server;
		//public SocketPolicyServer PolicyServer;
		//public NetPeerConfiguration Config;

		public Dictionary<WebSocket, NetworkConnection> ConnectionLookup = new(NetworkConfig.InitialConnectionCapacity);
		public List<NetworkConnection> Players = new();

		public List<NetworkConnection> DisconnectList = new(5);

		public Action<InboundMessage>[] PacketHandlers;

		public World World;

		public PacketType LastPacketType;

		public bool DebugMode;
	}
}
