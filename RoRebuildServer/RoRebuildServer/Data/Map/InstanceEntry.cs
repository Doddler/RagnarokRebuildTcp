namespace RoRebuildServer.Data.Map;

public class InstanceEntry
{
    public required string Name { get; set; }
    public required bool IsWorldInstance { get; set; }
    public required List<string> Maps { get; set; }
}