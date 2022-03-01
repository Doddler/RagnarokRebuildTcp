namespace RoRebuildServer.Data.Map;

public class ServerMapConfigAttribute : Attribute
{
    public readonly string MapName;

    public ServerMapConfigAttribute(string mapName)
    {
        MapName = mapName;
    }
}