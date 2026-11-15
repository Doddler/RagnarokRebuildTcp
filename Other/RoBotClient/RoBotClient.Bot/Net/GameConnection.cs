using System.Net.WebSockets;
using RebuildSharedData.Networking;
using RoBotClient.Bot.Protocol;

namespace RoBotClient.Bot.Net;

/// <summary>
/// Low-level WebSocket transport to the game server. One binary frame == one packet.
/// Handles connect, framed send (guarded by a lock, since a WebSocket forbids concurrent sends),
/// and reading one full message at a time. Login/flow orchestration lives a layer above this.
/// </summary>
public sealed class GameConnection : IAsyncDisposable
{
    private readonly ClientWebSocket _ws = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public WebSocketState State => _ws.State;

    public Task ConnectAsync(Uri uri, CancellationToken ct = default) => _ws.ConnectAsync(uri, ct);

    /// <summary>Sends pre-framed bytes (used for the non-bit-packed login handshake).</summary>
    public Task SendRawAsync(byte[] data, CancellationToken ct = default) => SendMemoryAsync(data, ct);

    public Task SendPacketAsync(PacketWriter w, CancellationToken ct = default) => SendMemoryAsync(w.AsMemory(), ct);

    public Task SendSimpleAsync(PacketType type, CancellationToken ct = default)
    {
        var w = new PacketWriter(8);
        w.WritePacketType(type);
        return SendPacketAsync(w, ct);
    }

    private async Task SendMemoryAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        await _sendLock.WaitAsync(ct);
        try
        {
            await _ws.SendAsync(data, WebSocketMessageType.Binary, endOfMessage: true, ct);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>Reads one full WebSocket message (one packet); returns null if the socket closed.</summary>
    public async Task<byte[]?> ReceiveFrameAsync(CancellationToken ct = default)
    {
        var buffer = new byte[8192];
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", CancellationToken.None);
                return null;
            }
            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        return ms.ToArray();
    }

    public async Task<PacketReader?> ReceivePacketAsync(CancellationToken ct = default)
    {
        var bytes = await ReceiveFrameAsync(ct);
        return bytes == null ? null : new PacketReader(bytes, bytes.Length);
    }

    public async Task CloseAsync()
    {
        if (_ws.State == WebSocketState.Open)
        {
            try { await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None); }
            catch { /* socket may already be tearing down */ }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
        _ws.Dispose();
        _sendLock.Dispose();
    }
}
