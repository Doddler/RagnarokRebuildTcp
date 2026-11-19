using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using MemoryPack;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RebuildSharedData.Packets;
using RoBotClient.Bot.Protocol;
using RoBotClient.Bot.Session;

namespace RoBotClient.Web.Features.Watch;

/// <summary>
/// One per bot session: keeps just enough of the bot's raw packet stream to bring a late-joining Unity client up to
/// date (how it entered the world, its latest player data, the create packet of every entity and drop in view), and
/// forwards every new packet to the Unity clients watching it.
/// </summary>
public sealed class PovRecorder
{
    private static readonly ConditionalWeakTable<BotSession, PovRecorder> All = new();

    /// <summary>Start recording every bot session. Call once at startup, before bots log in.</summary>
    public static void Install() =>
        BotSession.PacketTap += (session, buffer, length) => All.GetValue(session, s => new PovRecorder(s)).Record(buffer, length);

    public static PovRecorder? For(BotSession session) => All.TryGetValue(session, out var r) ? r : null;

    private readonly object _lock = new();
    private readonly BotSession _session;
    private byte[]? _enterServer;
    private byte[]? _changeMaps;
    private byte[]? _playerData;
    private string _map = "";
    private readonly Dictionary<int, byte[]> _entities = new(); // latest CreateEntity2 per entity id
    private readonly Dictionary<int, byte[]> _drops = new();    // DropItem per drop id
    private readonly List<Viewer> _viewers = new();

    private PovRecorder(BotSession session) => _session = session;

    public int ViewerCount { get { lock (_lock) return _viewers.Count; } }

    private void Record(byte[] buffer, int length)
    {
        var packet = buffer.AsSpan(0, length).ToArray();
        var r = new PacketReader(packet, length);
        var type = r.ReadPacketType();
        lock (_lock)
        {
            switch (type)
            {
                case PacketType.EnterServer:
                    _enterServer = packet;
                    r.ReadInt32();
                    EnterMap(r.ReadString());
                    break;
                case PacketType.ChangeMaps:
                    _changeMaps = packet;
                    EnterMap(r.ReadString());
                    break;
                case PacketType.UpdatePlayerData: _playerData = packet; break;
                case PacketType.CreateEntity2: _entities[ReadSpawn(packet).Spawn.ServerId] = packet; break;
                case PacketType.RemoveEntity: _entities.Remove(r.ReadInt32()); break;
                case PacketType.RemoveAllEntities: _entities.Clear(); break;
                case PacketType.DropItem: _drops[r.ReadInt32()] = packet; break;
                case PacketType.PickUpItem:
                    r.ReadInt32(); // picker
                    _drops.Remove(r.ReadInt32());
                    break;
            }

            foreach (var viewer in _viewers)
                viewer.Forward(packet, type, _map);
        }
    }

    private void EnterMap(string map)
    {
        _map = map;
        _entities.Clear();
        _drops.Clear();
    }

    /// <summary>Serve one Unity client until it disconnects. It has already sent its login packet.</summary>
    public async Task WatchAsync(WebSocket socket, CancellationToken ct)
    {
        var viewer = new Viewer(socket);
        lock (_lock)
        {
            if (_enterServer == null)
                return;
            viewer.Send(EnterServerOn(_map)); // the client loads the map, then sends PlayerReady
            if (_playerData != null)
                viewer.Send(_playerData);
            viewer.Map = _map;
            _viewers.Add(viewer);
        }

        var pump = viewer.PumpAsync(ct);
        var buffer = new byte[4096];
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;
                // Everything else the client sends (moves, skills, chat, pings) is dropped: it only watches.
                if (result.Count > 0 && (PacketType)buffer[0] == PacketType.PlayerReady)
                    OnViewerReady(viewer);
            }
        }
        catch (Exception) when (ct.IsCancellationRequested || socket.State != WebSocketState.Open) { }
        finally
        {
            lock (_lock)
                _viewers.Remove(viewer);
            viewer.Close();
            await pump;
        }
    }

    /// <summary>The viewer finished loading a map: if the bot has moved on since, send it there too, otherwise
    /// catch it up with what's in view and go live.</summary>
    private void OnViewerReady(Viewer viewer)
    {
        lock (_lock)
        {
            if (viewer.Map != _map && _changeMaps != null)
            {
                viewer.Send(_changeMaps);
                viewer.Map = _map;
                return;
            }

            var views = _session.WithState(w => w.Entities.Values.ToDictionary(e => e.Id, e => e));
            foreach (var (id, create) in _entities)
                viewer.Send(views.TryGetValue(id, out var view)
                    ? CreateAt(create, view.EstimatedCell(), view.State, view.Facing, view.Hp, view.MaxHp)
                    : create);
            foreach (var drop in _drops.Values)
                viewer.Send(drop);
            viewer.Live = true;
        }
    }

    /// <summary>The bot's EnterServer packet, but on the map it's on now.</summary>
    private byte[] EnterServerOn(string map)
    {
        var r = new PacketReader(_enterServer!, _enterServer!.Length);
        r.ReadPacketType();
        var id = r.ReadInt32();
        r.ReadString();
        var guid = r.ReadBytes(16);
        var w = new PacketWriter();
        w.WritePacketType(PacketType.EnterServer);
        w.Write(id);
        w.Write(map);
        w.Write(guid);
        return w.ToArray();
    }

    private static (EntitySpawnParameters Spawn, int TailStart) ReadSpawn(byte[] packet)
    {
        var r = new PacketReader(packet, packet.Length);
        r.ReadPacketType();
        if ((CreateEntityEventType)r.ReadByte() == CreateEntityEventType.Toss)
            r.ReadPosition();
        var spawn = r.MemoryPackDeserializeWithLength<EntitySpawnParameters>();
        return (spawn, r.Position / 8); // the blob ends byte-aligned; any player/NPC blob follows
    }

    /// <summary>A CreateEntity2 as the server would send it now: the entity's current cell, state and HP (an idle
    /// monster would otherwise pop up where it was first seen). Moving is sent as idle, the walk path isn't kept.</summary>
    private static byte[] CreateAt(byte[] create, RebuildSharedData.Data.Position cell, CharacterState state, Direction facing, int hp, int maxHp)
    {
        var (spawn, tailStart) = ReadSpawn(create);
        spawn.Position = cell;
        spawn.State = state == CharacterState.Moving ? CharacterState.Idle : state;
        spawn.Facing = facing;
        if (maxHp > 0)
        {
            spawn.Hp = hp;
            spawn.MaxHp = maxHp;
        }

        // Byte-aligned layout (as OutboundMessage writes it): type, event type, int32 length, blob, rest unchanged.
        var blob = MemoryPackSerializer.Serialize(spawn);
        using var ms = new MemoryStream();
        ms.WriteByte((byte)PacketType.CreateEntity2);
        ms.WriteByte((byte)CreateEntityEventType.Normal);
        ms.Write(BitConverter.GetBytes(blob.Length));
        ms.Write(blob);
        ms.Write(create, tailStart, create.Length - tailStart);
        return ms.ToArray();
    }

    /// <summary>One watching Unity client: an ordered outbox drained to its socket.</summary>
    private sealed class Viewer(WebSocket socket)
    {
        private readonly Channel<byte[]> _outbox = Channel.CreateUnbounded<byte[]>(new() { SingleReader = true });
        public bool Live;      // map loaded and caught up: gets every packet the bot gets
        public string Map = ""; // the map this client was last sent to

        public void Send(byte[] packet) => _outbox.Writer.TryWrite(packet);

        public void Forward(byte[] packet, PacketType type, string botMap)
        {
            if (!Live)
                return;
            Send(packet);
            if (type is PacketType.ChangeMaps or PacketType.EnterServer)
            {
                Live = false; // the client loads the new map; its PlayerReady catches it up again
                Map = botMap;
            }
        }

        public void Close() => _outbox.Writer.TryComplete();

        public async Task PumpAsync(CancellationToken ct)
        {
            try
            {
                await foreach (var packet in _outbox.Reader.ReadAllAsync(ct))
                    await socket.SendAsync(packet, WebSocketMessageType.Binary, true, ct);
            }
            catch (Exception) { } // socket closed: WatchAsync's receive loop ends too
        }
    }
}
