using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using Leopotam.Ecs;
using Lidgren.Network;
using RebuildZoneServer.EntityComponents;
using RebuildZoneServer.Networking.Enum;

namespace RebuildZoneServer.Networking
{
	public class NetworkConnection
	{
		//public NetConnection ClientConnection;
        public WebSocket Socket;
		public ConnectionStatus Status;
		public EcsEntity Entity;
		public Character Character;
		public Player Player;
		public double LastKeepAlive;
        public bool Confirmed = false;
        public CancellationToken Cancellation;
        public CancellationTokenSource CancellationSource;

        public NetworkConnection(WebSocket socket)
        {
            Socket = socket;
            CancellationSource = new CancellationTokenSource();
            Cancellation = CancellationSource.Token;
        }

		//public NetworkConnection(NetConnection connection)
		//{
		//	ClientConnection = connection;
		//}
	}
}
