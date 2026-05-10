namespace RoRebuildServer.Data.MapData;

public class ServerMapConfigAttribute : Attribute
{
    public readonly string MapName;

    public ServerMapConfigAttribute(string mapName)
    {
        MapName = mapName;
    }
}