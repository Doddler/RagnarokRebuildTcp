namespace RebuildSharedData.ClientTypes;

#pragma warning disable CS8618 //disable warning for nullable fields

[Serializable]
public class PortalEntry
{
    public string To;
    public int X;
    public int Y;
}

[Serializable]
public class MapWarpEntry
{
    public string Map;
    public List<string> ConnectedTo;
    public List<PortalEntry> Portals;
}

[Serializable]
public class MapWarpFile
{
    public List<MapWarpEntry> Items;
}
