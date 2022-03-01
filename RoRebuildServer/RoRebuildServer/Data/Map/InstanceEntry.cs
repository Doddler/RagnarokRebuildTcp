namespace RoRebuildServer.Data.Map;

public class InstanceEntry
{
    public string Name { get; set; }
    public bool IsWorldInstance { get; set; }
    public List<string> Maps { get; set; }
}