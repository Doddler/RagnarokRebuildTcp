using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Lidgren.Network;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.ObjectPool;
using RebuildData.Server.Logging;
using RebuildData.Shared.Enum;
using RebuildData.Shared.Networking;
using RebuildZoneServer.Data.Management;
using RebuildZoneServer.EntityComponents;
using RebuildZoneServer.Sim;
using RebuildZoneServer.Util;

namespace RebuildZoneServer.Networking
{
    public static class NetworkManager
    {
        public static ServerState State;

        public static int PlayerCount => State.ConnectionLookup.Count;
        private static NetQueue<InboundMessage> inboundChannel;
        private static NetQueue<OutboundMessage> outboundChannel;
        private static Channel<NetworkConnection> disconnectList;
        private static ObjectPool<OutboundMessage> outboundPool;
        private static ObjectPool<InboundMessage> inboundPool;

        private static ReaderWriterLockSlim clientLock = new();
        
        private static Thread outboundMessageThread;

        private static bool isRunning = true;
        public static bool IsSingleThreadMode = false;
        public static bool QuickMoveEnabled = false;

        public static void Init(World gameWorld)
        {
            State = new ServerState();
            State.World = gameWorld;

            //policy server is required for web build, but since webGL doesn't support lidgren, it's disabled
            //StartPolicyServer();

            if (!DataManager.TryGetConfigInt("Port", out var port))
                throw new Exception("Configuration does not have value for port!");
            if (!DataManager.TryGetConfigInt("MaxConnections", out var maxConnections))
                throw new Exception("Configuration does not have value for max connections!");


            if (DataManager.TryGetConfigInt("SingleThread", out var threading))
                IsSingleThreadMode = threading != 0;

            if (DataManager.TryGetConfigInt("QuickMoveEnabled", out var quickmove))
                QuickMoveEnabled = quickmove != 0;
            
            if (DataManager.TryGetConfigInt("Debug", out var debug))
                State.DebugMode = debug == 1;

#if DEBUG
			State.DebugMode = true;
#else
            if(State.DebugMode)
                ServerLogger.LogWarning("Server is started using debug mode config flag! Be sure this is what you want.");
#endif

            ServerLogger.Log(
                $"Starting server with a maximum of {maxConnections} connections.");

            inboundChannel = new NetQueue<InboundMessage>(100);
            outboundChannel = new NetQueue<OutboundMessage>(100);
            disconnectList = Channel.CreateUnbounded<NetworkConnection>(new UnboundedChannelOptions
            { SingleReader = true, SingleWriter = false });

            outboundPool = new DefaultObjectPool<OutboundMessage>(new DefaultPooledObjectPolicy<OutboundMessage>(), 10);
            inboundPool = new DefaultObjectPool<InboundMessage>(new DefaultPooledObjectPolicy<InboundMessage>(), 10);

            if (!IsSingleThreadMode)
            {
                ServerLogger.Log("Starting messaging thread...");
                outboundMessageThread = new Thread(ProcessOutgoingMessagesThread);
                outboundMessageThread.Priority = ThreadPriority.AboveNormal;
                outboundMessageThread.Start();
            }
            else
                ServerLogger.Log("Starting in single thread mode.");

            var handlerCount = System.Enum.GetNames(typeof(PacketType)).Length;
            State.PacketHandlers = new Action<InboundMessage>[handlerCount];

            foreach (var type in Assembly.GetAssembly(typeof(NetworkManager)).GetTypes()
                .Where(t => t.IsClass && t.IsSubclassOf(typeof(ClientPacketHandler))))
            {
                var handler = (ClientPacketHandler)Activator.CreateInstance(type);
                var packetType = handler.PacketType;
                handler.State = State;

                if (State.PacketHandlers[(int)packetType] != null)
                    throw new Exception($"Duplicate packet handler exists for type {packetType}!");

                if(packetType == PacketType.UnhandledPacket || packetType == PacketType.Disconnect)
                    State.PacketHandlers[(int)packetType] = handler.HandlePacketNoCheck; //skip client connected check for these two packets
                else
                    State.PacketHandlers[(int)packetType] = handler.HandlePacket;
            }

            for (var i = 0; i < handlerCount; i++)
            {
                if (State.PacketHandlers[i] == null)
                {

                    ServerLogger.Debug($"No packet handler for packet type PacketType.{(PacketType)i} exists.");
                    State.PacketHandlers[i] = State.PacketHandlers[(int)PacketType.UnhandledPacket];
                }
            }

            isRunning = true;

            ServerLogger.Log("Server started.");
        }

        public static void Shutdown()
        {
            var players = State.Players;
            for (var i = 0; i < players.Count; i++)
            {
                DisconnectPlayer(players[i]);
            }
        }
        
        public static async Task ScanAndDisconnect()
        {
            var players = State.Players;
            
            clientLock.EnterReadLock();

            try
            {
                for (var i = 0; i < players.Count; i++)
                {
                    if (players[i].Socket.State != WebSocketState.Open &&
                        players[i].Socket.State != WebSocketState.Connecting)
                        await disconnectList.Writer.WriteAsync(players[i]);
                    else
                    {
                        if (players[i].Character == null)
                        {
                            if (players[i].LastKeepAlive + 20 < Time.ElapsedTime)
                                await disconnectList.Writer.WriteAsync(players[i]);
                        }
                        else
                        {
                            if (players[i].Character.IsActive && players[i].LastKeepAlive + 20 < Time.ElapsedTime)
                                await disconnectList.Writer.WriteAsync(players[i]);
                            if (!players[i].Character.IsActive && players[i].LastKeepAlive + 120 < Time.ElapsedTime)
                                await disconnectList.Writer.WriteAsync(players[i]);
                        }
                    }
                }
            }
            finally
            {
                clientLock.ExitReadLock();
            }
            
            while (disconnectList.Reader.TryRead(out var dc))
            {
                ServerLogger.Log($"[Network] Player {dc.Entity} has disconnected, removing from world.");
                DisconnectPlayer(dc);
            }
        }

        public static InboundMessage CreateInboundMessage(NetworkConnection client)
        {
            var obj = inboundPool.Get();
            if (client != null)
                obj.Client = client;
            return obj;
        }

        public static OutboundMessage CreateOutboundMessage(NetworkConnection client = null)
        {
            var obj = outboundPool.Get();
            if (client != null)
                obj.Clients.Add(client);
            return obj;
        }

        public static void RetireOutboundMessage(OutboundMessage message)
        {
            message.Clear();
            outboundPool.Return(message);
        }
        
        public static void RetireInboundMessage(InboundMessage message)
        {
            message.Clear();
            inboundPool.Return(message);
        }

        public static async Task ProcessIncomingMessages()
        {
            while (inboundChannel.TryDequeue(out var item))
            {
                try
                {
                    if (item.Client.Confirmed)
                    {
                        HandleMessage(item);
                    }
                    else
                    {
                        ServerLogger.Log("Ignoring message from non-confirmed client...");
                    }
                    
                }
                catch (Exception e)
                {
                    ServerLogger.LogWarning("Received invalid packet which generated an exception. Error: " +
                                            e.Message);

                    if (item.Client != null)
                        await disconnectList.Writer.WriteAsync(item.Client, CancellationToken.None);
                }
                finally
                {
                    RetireInboundMessage(item);
                }
            }
        }

        private static void ProcessOutgoingMessagesThread()
        {
            Task.Run(ProcessOutgoingMessagesLoop);
        }

        public static async Task ProcessOutgoingMessages()
        {
            while (outboundChannel.TryDequeue(out var message))
            {
                //var message = await outboundChannel.Reader.ReadAsync();
                foreach (var client in message.Clients)
                {
                    if (client.Socket.State == WebSocketState.Open)
                    {
                        //var timeoutToken = new CancellationTokenSource(15000).Token;
                        try
                        {
                            await client.Socket.SendAsync(
                                new ArraySegment<byte>(message.Message, 0, message.Length),
                                WebSocketMessageType.Binary, true, client.Cancellation);
                        }
                        catch
                        {
                            ServerLogger.LogWarning($"Client {client.Entity} failed to receive packet.");
                            await disconnectList.Writer.WriteAsync(client);
                        }
                    }
                }
            }

        }

        private static async Task ProcessOutgoingMessagesLoop()
        {
            while (isRunning)
            {
                await ProcessOutgoingMessages();
                await Task.Delay(1);
            }
        }
        
        public static void DisconnectPlayer(NetworkConnection connection)
        {
            if (connection == null)
            {
                ServerLogger.LogError("Cannot disconnect player when connection is null. This is probably a sign of very bad stuff happening.");
                return;
            }

            clientLock.EnterReadLock();

            try
            {
                if (connection.Entity.IsAlive())
                {
                    var player = connection.Entity.Get<Player>();
                    var combatEntity = connection.Entity.Get<CombatEntity>();

                    connection.Character.Map?.RemoveEntity(ref connection.Entity, CharacterRemovalReason.Disconnect);
                    
                    //connection.ClientConnection.Disconnect("Thanks for playing!");
                    
                    State.World.RemoveEntity(ref connection.Entity);
                }


                if (State.ConnectionLookup.ContainsKey(connection.Socket))
                    State.ConnectionLookup.Remove(connection.Socket);

                if (State.Players.Contains(connection))
                    State.Players.Remove(connection);

                connection.CancellationSource.Cancel();

            }
            finally
            {
                clientLock.ExitReadLock();
            }
        }

        public static void HandleMessage(InboundMessage msg)
        {
            var type = (PacketType)msg.ReadByte();
#if DEBUG
			if(State.ConnectionLookup.TryGetValue(msg.Client.Socket, out var connection) && connection.Entity.IsAlive())
				ServerLogger.Debug($"Received message of type: {System.Enum.GetName(typeof(PacketType), type)} from entity {connection.Entity}.");
			else
				ServerLogger.Debug($"Received message of type: {System.Enum.GetName(typeof(PacketType), type)} from entity-less connection.");

			State.LastPacketType = type;
			State.PacketHandlers[(int)type](msg);
#endif
#if !DEBUG
            try
            {
                State.LastPacketType = type;
                State.PacketHandlers[(int)type](msg);
            }
            catch (Exception)
            {
                ServerLogger.LogError($"Error executing packet handler for packet type {type}");
                throw;
            }
#endif
        }

        public static void SendMessage(OutboundMessage message, NetworkConnection connection)
        {
            if(message.Clients.Count == 0 || !message.Clients.Contains(connection))
                message.Clients.Add(connection);

            outboundChannel.Enqueue(message);
        }

        public static void SendMessageMulti(OutboundMessage message, List<NetworkConnection> connections)
        {
            for (var i = 0; i < connections.Count; i++)
            {
                var c = connections[i];
                if(!message.Clients.Contains(c))
                    message.Clients.Add(c);
            }
            
            if(message.Clients.Count > 0)
                outboundChannel.Enqueue(message);
        }

        public static OutboundMessage StartPacket(PacketType type, int capacity = 0)
        {
            var msg = CreateOutboundMessage();
            msg.WritePacketType(type);

            return msg;
        }

        public static async Task ReceiveConnection(HttpContext context, WebSocket socket)
        {
            var buffer = new byte[1024 * 4];
            var timeoutToken = new CancellationTokenSource(15000).Token;
            WebSocketReceiveResult result;

            ServerLogger.Log("We're seeing a new connection!");

            try
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeoutToken);

            }
            catch (OperationCanceledException)
            {
                ServerLogger.Log("New user attempted to connect, but timed out.");
                return;
            }

            var txt = Encoding.UTF8.GetString(new ReadOnlySpan<byte>(buffer, 0, result.Count));
            
            if (txt != "Connect")
            {
                ServerLogger.Log("Client failed to connect properly, disconnecting...");
                timeoutToken = new CancellationTokenSource(15000).Token;
                await socket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                return;
            }
            
            var playerConnection = new NetworkConnection(socket);
            playerConnection.LastKeepAlive = Time.ElapsedTime + 20;
            playerConnection.Confirmed = true;

            var cancellation = playerConnection.Cancellation;

            ServerLogger.Log($"We have a new connection!");
            
            clientLock.EnterWriteLock();

            try
            {
                State.ConnectionLookup.Add(socket, playerConnection);
                State.Players.Add(playerConnection);
            }
            finally
            {
                clientLock.ExitWriteLock();
            }

            var msg = CreateOutboundMessage(playerConnection);
            msg.WritePacketType(PacketType.ConnectionApproved);
            outboundChannel.Enqueue(msg);

            while (socket.State == WebSocketState.Open)
            {
                var exit = false;

                try
                {
                    //timeoutToken = new CancellationTokenSource(15000).Token;
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellation);
                }
                catch (OperationCanceledException)
                {
                    ServerLogger.Log($"Client has timed out, disconnecting player.");
                    exit = true;
                }
                catch(Exception e)
                {
                    ServerLogger.Log($"Client caused an exception when receiving, disconnecting player. Exception: " + e.Message);
                    exit = true;
                }

                if (exit)
                    break;
                
                var inMsg = CreateInboundMessage(playerConnection);
                inMsg.Populate(buffer, 0, result.Count);

                //Buffer.BlockCopy(buffer, 0, inMsg, 0, result.Count);

                inboundChannel.Enqueue(inMsg);
            }

            //timeoutToken = new CancellationTokenSource(15000).Token;

            if (socket.State == WebSocketState.Open && result.CloseStatus != null)
                await socket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, cancellation);

            await disconnectList.Writer.WriteAsync(playerConnection);
        }
    }
}
