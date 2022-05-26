namespace RebuildSharedData.ClientTypes;

[Serializable]
public class ClientMapEntry
{
    public string Code;
    public string Name;
    public string MapMode;
    public string Music;
}

[Serializable]
public class ClientMapList
{
    public List<ClientMapEntry> MapEntries;
}