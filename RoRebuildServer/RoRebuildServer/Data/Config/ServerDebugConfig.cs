namespace RoRebuildServer.Data.Config;

public class ServerDebugConfig
{
    public bool UseDebugMode { get; set; }
    public bool EnableWarpCommandForEveryone { get; set; }
    public bool EnableEnterSpecificMap { get; set; }
    public int MaxSpawnTime { get; set; }
    public bool UseForceEnterMap { get; set; }
    public string ForceEnterMapName { get; set; }
}