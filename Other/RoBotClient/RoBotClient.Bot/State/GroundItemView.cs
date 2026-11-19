namespace RoBotClient.Bot.State;

/// <summary>An item lying on the ground, as reported by the server's DropItem packet.</summary>
public sealed class GroundItemView
{
    public int DropId;   // unique ground-item id — what you send to pick it up
    public int ItemId;   // item type id — for blacklist + name resolution
    public int Count;
    public int X;        // tile (truncated from the float drop position)
    public int Y;
}
