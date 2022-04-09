using System.Net.WebSockets;
using RoRebuildServer.Database.Requests;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntitySystem;

namespace RoRebuildServer.Networking;

public enum ConnectionStatus
{
    PendingAuthentication,
    Connected
}

public class NetworkConnection
{
    public WebSocket Socket { get; set; }
    public ConnectionStatus Status { get; set; }
    public Entity Entity;
    public WorldObject? Character { get; set; }
    public Player? Player { get; set; }
    public double LastKeepAlive { get; set; }
    public bool Confirmed { get; set; } = false;
    public CancellationToken Cancellation { get; set; }
    public CancellationTokenSource CancellationSource { get; set; }
    public LoadCharacterRequest? LoadCharacterRequest { get; set; }

    public NetworkConnection(WebSocket socket)
    {
        Socket = socket;
        CancellationSource = new CancellationTokenSource();
        Cancellation = CancellationSource.Token;
    }
}