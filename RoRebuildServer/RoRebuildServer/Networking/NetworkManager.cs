using System.Buffers;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading.Channels;
using Lidgren.Network;
using Microsoft.Extensions.ObjectPool;
using Microsoft.VisualBasic;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RebuildZoneServer.Networking;
using RoRebuildServer.Data;
using RoRebuildServer.Database;
using RoRebuildServer.Database.Requests;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Logging;
using RoRebuildServer.Simulation;
using RoRebuildServer.Simulation.Util;

#pragma warning disable CS8618 //non nullable member is uninitialized

namespace RoRebuildServer.Networking;

public class NetworkManager
{
    public static World World { get; private set; }

    public static int PlayerCount => ConnectionLookup.Count;

    public static Dictionary<WebSocket, NetworkConnection> ConnectionLookup = new(ServerConfig.InitialConnectionCapacity);
    public static List<NetworkConnection> Players = new();
    public static List<NetworkConnection> DisconnectList = new(5);
    public static Action<NetworkConnection, InboundMessage>[] PacketHandlers;
    public static bool[] PacketCheckClientState;
    public static PacketType LastPacketType;
    public static Dictionary<int, bool> AdminPacketLookup = new();

    private static NetQueue<InboundMessage> inboundChannel;
    private static NetQueue<OutboundMessage> outboundChannel;
    private static Channel<NetworkConnection> disconnectList;
    private static ObjectPool<OutboundMessage> outboundPool;
    private static ObjectPool<InboundMessage> inboundPool;

#if DEBUG
    private static PriorityQueue<InboundMessage, float> inboundLagSimQueue = new();
    private static PriorityQueue<OutboundMessage, float> outboundLagSimQueue = new();
    private static Object inboundLagLock = new();
    private static Object outboundLagLock = new();
    private static bool useSimulatedLag;
    private static float inboundLagTime;
    private static float outboundLagTime;
#endif

    private static ReaderWriterLockSlim clientLock = new();
    //private static Thread outboundMessageThread;

    //public static int PlayerCount => State.ConnectionLookup.Count;

    private static int clientTimeoutTime = ServerConfig.OperationConfig.ClientTimeoutSeconds;

    public static bool IsRunning;
    public static bool IsSingleThreadMode { get; set; }
    public static bool DebugMode;

    public static void Init(World gameWorld)
    {
        World = gameWorld;
        
        IsSingleThreadMode = !ServerConfig.OperationConfig.UseMultipleThreads;
        var debugConfig = ServerConfig.DebugConfig;
        
        DebugMode = debugConfig.UseDebugMode;

#if DEBUG
        DebugMode = true;

        //simulated lag stuff. Only used in debug mode
        useSimulatedLag = debugConfig.AddSimulatedLag;
        inboundLagTime = debugConfig.InboundSimulatedLag / 1000f;
        outboundLagTime = debugConfig.OutboundSimulatedLag / 1000f;
        if(useSimulatedLag && inboundLagTime > 0f)
            ServerLogger.Log($"Simulated inbound lag enabled with a lag time of {debugConfig.InboundSimulatedLag}ms.");
        if (useSimulatedLag && outboundLagTime > 0f)
        {
            ServerLogger.Log($"Simulated outbound lag enabled with a lag time of {debugConfig.OutboundSimulatedLag}ms.");
            Task.Run(OutboundLagBackgroundThread).ConfigureAwait(false);
        }
#else
            if(DebugMode)
                ServerLogger.LogWarning("Server is started using debug mode config flag! Be sure this is what you want.");
#endif


        ServerLogger.Log($"Starting server NetworkManager!");


        inboundChannel = new NetQueue<InboundMessage>(100);
        outboundChannel = new NetQueue<OutboundMessage>(100);
        disconnectList = Channel.CreateUnbounded<NetworkConnection>(new UnboundedChannelOptions
            { SingleReader = true, SingleWriter = false });

        outboundPool = new DefaultObjectPool<OutboundMessage>(new OutboundMessagePooledObjectPolicy(), 10);
        inboundPool = new DefaultObjectPool<InboundMessage>(new DefaultPooledObjectPolicy<InboundMessage>(), 10);
        
        if (!IsSingleThreadMode)
        {
            ServerLogger.Log("Starting messaging thread...");
            //outboundMessageThread = new Thread(ProcessOutgoingMessagesThread);
            //outboundMessageThread.Priority = ThreadPriority.AboveNormal;
            //outboundMessageThread.Start();
            //ProcessOutgoingMessagesThread();
            Task.Run(ProcessOutgoingMessagesLoop).ConfigureAwait(false);
        }
        else
            ServerLogger.Log("Starting in single thread mode.");

        var handlerCount = System.Enum.GetNames(typeof(PacketType)).Length;
        PacketHandlers = new Action<NetworkConnection, InboundMessage>[handlerCount];
        PacketCheckClientState = new bool[handlerCount];
        
        foreach (var type in Assembly.GetAssembly(typeof(NetworkManager))!.GetTypes()
                     .Where(t => t.IsClass && t.GetCustomAttribute<ClientPacketHandlerAttribute>() != null))
        {
            var handler = (IClientPacketHandler)Activator.CreateInstance(type)!;
            var attr = type.GetCustomAttribute<ClientPacketHandlerAttribute>();
            var packetType = attr!.PacketType;
            
            if (PacketHandlers[(int)packetType] != null)
                throw new Exception($"Duplicate packet handler exists for type {packetType}!");

            PacketCheckClientState[(int)packetType] = attr.VerifyClientConnection;

            if (attr.IsAdminPacket)
                AdminPacketLookup[(int)packetType] = true;

            //if (packetType == PacketType.UnhandledPacket || packetType == PacketType.Disconnect)
            //    PacketHandlers[(int)packetType] = handler.HandlePacketNoCheck; //skip client connected check for these two packets

            PacketHandlers[(int)packetType] = handler.Process;
        }

        for (var i = 0; i < handlerCount; i++)
        {
            if (PacketHandlers[i] == null)
            {
                var type = typeof(PacketType);
                var ptype = (PacketType)i;
                var member = type.GetMember(ptype.ToString());

                //only complain about packets not marked with ServerOnlyPacket
                if(member[0].GetCustomAttribute<ServerOnlyPacketAttribute>() == null)
                    ServerLogger.Debug($"No packet handler for packet type PacketType.{(PacketType)i} exists.");

                PacketHandlers[i] = PacketHandlers[(int)PacketType.UnhandledPacket];
            }
        }

        if (clientTimeoutTime < 10)
            clientTimeoutTime = 10;

        IsRunning = true;

        ServerLogger.Log("Server started.");
    }

    public static void Shutdown()
    {
        var players = Players;
        for (var i = 0; i < players.Count; i++)
        {
            DisconnectPlayer(players[i]);
        }
    }

    public static void AddAllPlayersAsRecipient()
    {
        var players = Players;
        clientLock.EnterReadLock();
        
        try
        {
            for (var i = 0; i < players.Count; i++)
            {
                if (players[i].Socket.State == WebSocketState.Open)
                    CommandBuilder.AddRecipient(players[i].Entity);
            }
        }
        finally
        {
            clientLock.ExitReadLock();
        }
    }

    //useful if the server is going to shit itself for a bit and you don't want players timing out
    public static void ExtendTimeoutForAllPlayers(int seconds)
    {
        var players = Players;
        clientLock.EnterReadLock();

        try
        {
            for (var i = 0; i < players.Count; i++)
            {
                if (players[i].Socket.State == WebSocketState.Open)
                    players[i].LastKeepAlive += (double)seconds;
            }
        }
        finally
        {
            clientLock.ExitReadLock();
        }
    }

    public static async Task ScanAndDisconnect()
    {
        var players = Players;

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
                    var chara = players[i].Character;
                    if (chara == null)
                    {
                        if (players[i].LastKeepAlive + clientTimeoutTime < Time.ElapsedTime)
                            await disconnectList.Writer.WriteAsync(players[i]);
                    }
                    else
                    {
                        if (chara.IsActive && players[i].LastKeepAlive + clientTimeoutTime < Time.ElapsedTime)
                            await disconnectList.Writer.WriteAsync(players[i]);
                        if (!chara.IsActive && players[i].LastKeepAlive + clientTimeoutTime + 120 < Time.ElapsedTime)
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

    public static void QueueDisconnect(NetworkConnection connection)
    {
        connection.CancellationSource.Cancel();
    }

    public static void DisconnectPlayer(NetworkConnection connection)
    {
        if (connection == null)
        {
            ServerLogger.LogError("Cannot disconnect player when connection is null. This is probably a sign of very bad stuff happening.");
            return;
        }

        clientLock.EnterWriteLock();

        try
        {
            if (connection.Entity.IsAlive())
            {
                //var player = connection.Entity.Get<Player>();
                //var combatEntity = connection.Entity.Get<CombatEntity>();

                //connection.Character.Map?.RemoveEntity(ref connection.Entity, CharacterRemovalReason.Disconnect, true);

                //connection.ClientConnection.Disconnect("Thanks for playing!");

                World.FullyRemoveEntity(ref connection.Entity);
            }


            if (ConnectionLookup.ContainsKey(connection.Socket))
                ConnectionLookup.Remove(connection.Socket);

            if (Players.Contains(connection))
                Players.Remove(connection);

            connection.CancellationSource.Cancel();

        }
        finally
        {
            clientLock.ExitWriteLock();
        }
    }

    public static InboundMessage CreateInboundMessage(NetworkConnection? client)
    {
        var obj = inboundPool.Get();
        if (client != null)
            obj.Client = client;
        return obj;
    }

    public static OutboundMessage CreateOutboundMessage(NetworkConnection? client = null)
    {
        var obj = outboundPool.Get();
        if (client != null)
            obj.Clients.Add(client);
        obj.IsInitialized = true;
        return obj;
    }

    public static void RetireOutboundMessage(OutboundMessage message)
    {
        //message.Clear();
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
#if DEBUG
        if (useSimulatedLag && inboundLagTime > 0f)
        {
            lock (inboundLagLock)
            {
                while (inboundLagSimQueue.TryPeek(out var msg, out var priority) && priority < Time.ElapsedTimeFloat)
                {
                    inboundLagSimQueue.Dequeue();
                    inboundChannel.Enqueue(msg);
                }
            }
        }
#endif
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
                ServerLogger.LogWarning("Received invalid packet which generated an exception. Error: " + e);

                if (item.Client != null)
                    await disconnectList.Writer.WriteAsync(item.Client, CancellationToken.None);
            }
            finally
            {
                RetireInboundMessage(item);
            }
        }
    }
#if DEBUG
    private static async Task OutboundLagBackgroundThread()
    {
        ServerLogger.Debug($"Using simulated lag, adding {(int)(inboundLagTime*1000)}ms to each inbound packet.");

        while (!IsRunning)
            await Task.Delay(500);

        while (IsRunning)
        {
            if (outboundLagSimQueue.TryPeek(out var msg, out var priority))
            {
                if (priority < Time.ElapsedTimeFloat)
                {
                    //in theory we could end up deque a different message from the one we peeked due to thread safety,
                    //but 
                    lock (outboundLagLock)
                        msg = outboundLagSimQueue.Dequeue();

                    if (msg == null)
                    {
                        ServerLogger.LogError($"OutboundLagBackgroundThread attempting to queue a message that is null");
                        continue;
                    }

                    outboundChannel.Enqueue(msg);
                }
                else
                    await Task.Delay(1);
            }
            else
            {
                if(PlayerCount <= 0)
                    await Task.Delay(1000);
                else
                    await Task.Delay(1);
            }
        }
    }
#endif

    //private static void ProcessOutgoingMessagesThread()
    //{
    //    Task.Run(ProcessOutgoingMessagesLoop).ConfigureAwait(false);
    //}
    private static async Task ProcessOutgoingMessagesLoop()
    {
        while (!IsRunning)
            await Task.Delay(1);
        
        while (IsRunning)
        {
            await ProcessOutgoingMessages();
            await Task.Delay(1);
        }

        ServerLogger.Debug("Ending outgoing message processing loop.");
    }

    public static async Task ProcessOutgoingMessages()
    {
        while (outboundChannel.TryDequeue(out var message))
        {
            if (message == null || !message.IsInitialized)
            {
                ServerLogger.LogError($"Server attempted to process outgoing message that was already recycled!");
                continue;
            }

            if (message.Clients == null || message.Clients.Count == 0)
            {
                ServerLogger.LogWarning($"Message type {(PacketType)message.Message[0]} in outbound queue, but it has no clients set as recipients.");
                RetireOutboundMessage(message);
                continue;
            }

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

            RetireOutboundMessage(message);
        }
    }
    
    public static void HandleMessage(InboundMessage msg)
    {
        if (msg.Length == 0)
            return;

        var type = (PacketType)msg.ReadByte();
#if DEBUG
        if (ConnectionLookup.TryGetValue(msg.Client.Socket, out var connection) && connection.Entity.IsAlive())
            ServerLogger.LogVerbose($"Received message of type: {System.Enum.GetName(typeof(PacketType), type)} from entity {connection.Entity}.");
        else
            ServerLogger.LogVerbose($"Received message of type: {System.Enum.GetName(typeof(PacketType), type)} from entity-less connection.");

        LastPacketType = type;

        if (PacketCheckClientState[(int)type])
        {
            if (msg.Client.Socket.State != WebSocketState.Open)
            {
                ServerLogger.Log("Ignoring message from non open web socket.");
                return;
            }
        }

        //if it's an admin packet and not an admin
        if (AdminPacketLookup.TryGetValue((int)type, out var isAdmin) && isAdmin && !connection.IsAdmin)
        {
            DisconnectPlayer(connection);
            ServerLogger.Log($"Player {connection.Character?.Name} is using an admin command without admin privileges. Disconnecting.");
            return;
        }

        PacketHandlers[(int)type](msg.Client, msg);
#endif
#if !DEBUG
            try
            {
                LastPacketType = type;
                if (PacketCheckClientState[(int)type])
                {
                    if (msg.Client.Socket.State != WebSocketState.Open)
                    {
                        ServerLogger.Log("Ignoring message from non open web socket.");
                        return;
                    }
                }

                PacketHandlers[(int)type](msg.Client, msg);
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
        if (message.Clients.Count == 0 || !message.Clients.Contains(connection))
            message.Clients.Add(connection);

        message.IsQueued = true;

#if DEBUG
        if (useSimulatedLag && outboundLagTime > 0f)
        {
            lock (outboundLagLock)
            {
                outboundLagSimQueue.Enqueue(message, Time.ElapsedTimeFloat + outboundLagTime);
            }
        }
        else
            outboundChannel.Enqueue(message);

#else
        outboundChannel.Enqueue(message);
#endif
    }

    public static void SendMessageMulti(OutboundMessage message, List<NetworkConnection>? connections)
    {
        if (connections == null || message == null)
            return;

        if(message.IsQueued)
            ServerLogger.LogError($"Attempting to send client message, but it's already queued to be sent!");

        for (var i = 0; i < connections.Count; i++)
        {
            var c = connections[i];
            if (!message.Clients.Contains(c))
                message.Clients.Add(c);
        }

        if (message.Clients.Count <= 0)
        {
            RetireOutboundMessage(message);
            return;
        }

        message.IsQueued = true;

#if DEBUG
        if (useSimulatedLag && outboundLagTime > 0f)
        {
            lock (outboundLagLock)
            {
                outboundLagSimQueue.Enqueue(message, Time.ElapsedTimeFloat + outboundLagTime);
            }
        }
        else
            outboundChannel.Enqueue(message);

#else
        outboundChannel.Enqueue(message);
#endif
    }
    
    public static OutboundMessage StartPacket(PacketType type, int capacity = 0)
    {
        var msg = CreateOutboundMessage();
        msg.WritePacketType(type);

        return msg;
    }

    private static bool GetCharId(string connectString, out Guid id)
    {
        var sp = connectString.AsSpan(7);
        return Guid.TryParse(sp, out id);
    }

    private static async Task<LoadCharacterRequest> LoadOrCreateCharacter(string connectString)
    {
        ServerLogger.Log("Received connection with connection string: " + connectString);
        if (connectString.Length > 7)
        {
            if (GetCharId(connectString, out var id))
            {
                var req = new LoadCharacterRequest(id);
                await RoDatabase.ExecuteDbRequestAsync(req);

                if (req.HasCharacter)
                {
                    ServerLogger.Log($"Client has an existing character! Character name {req.Name}.");
                    return req;
                }
            }
        }

        var name = "Player " + GameRandom.NextInclusive(0, 999);

        var charData = ArrayPool<int>.Shared.Rent((int)PlayerStat.PlayerStatsMax);

        var newReq = new SaveCharacterRequest(Guid.Empty, name, null, Position.Invalid, charData, new SavePosition());
        await RoDatabase.ExecuteDbRequestAsync(newReq);

        ArrayPool<int>.Shared.Return(charData, true);

        var loadReq = new LoadCharacterRequest(newReq.Id);
        await RoDatabase.ExecuteDbRequestAsync(loadReq);

        return loadReq;
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

        ServerLogger.Log(txt);

        if (!txt.StartsWith("Connect"))
        {
            ServerLogger.Log("Client failed to connect properly, disconnecting...");
            timeoutToken = new CancellationTokenSource(15000).Token;
            await socket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Server did not have valid connection string.", CancellationToken.None);
            return;
        }

        var playerConnection = new NetworkConnection(socket);
        playerConnection.LastKeepAlive = Time.ElapsedTime + 20;
        playerConnection.Confirmed = true;

        var cancellation = playerConnection.Cancellation;

        ServerLogger.Log($"We have a new connection!");

        //var hasCharacter = false;

        playerConnection.LoadCharacterRequest = await LoadOrCreateCharacter(txt);
        
        clientLock.EnterWriteLock();

        try
        {
            ConnectionLookup.Add(socket, playerConnection);
            Players.Add(playerConnection);
        }
        finally
        {
            clientLock.ExitWriteLock();
        }

        var msg = CreateOutboundMessage(playerConnection);
        msg.WritePacketType(PacketType.ConnectionApproved);
        playerConnection.Status = ConnectionStatus.Connected;
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
                ServerLogger.Log($"Client connection closed or timed out, disconnecting player.");
                exit = true;
            }
            catch (Exception e)
            {
                ServerLogger.Log($"Client caused an exception when receiving, disconnecting player. Exception: " + e.Message);
                exit = true;
            }

            if (exit)
                break;

            var inMsg = CreateInboundMessage(playerConnection);
            inMsg.Populate(buffer, 0, result.Count);

            //Buffer.BlockCopy(buffer, 0, inMsg, 0, result.Count);
#if DEBUG
            if (useSimulatedLag && inboundLagTime > 0f)
            {
                lock(inboundLagLock)
                    inboundLagSimQueue.Enqueue(inMsg, Time.ElapsedTimeFloat + inboundLagTime);
            }
            else
                inboundChannel.Enqueue(inMsg);
#else
            inboundChannel.Enqueue(inMsg);
#endif
        }

        playerConnection.Status = ConnectionStatus.Disconnected;

        //timeoutToken = new CancellationTokenSource(15000).Token;

        if (socket.State == WebSocketState.Open && result.CloseStatus != null)
            await socket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, cancellation);

        await disconnectList.Writer.WriteAsync(playerConnection);
    }

}