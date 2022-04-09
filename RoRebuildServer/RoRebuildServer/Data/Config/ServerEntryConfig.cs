using RebuildSharedData.Data;

namespace RoRebuildServer.Data.Config;

public class ServerEntryConfig
{
    public string Map { get; set; } = "prt_fild08";
    public int X { get; set; } = 170;
    public int Y { get; set; } = 367;
    public int Area { get; set; } = 0;

    public Position Position => new Position(X, Y);
}