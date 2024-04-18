namespace RoRebuildServer.Data.Config;

#pragma warning disable CS8618 //non nullable field defaulting to null

public class ServerDebugConfig
{
    public bool UseDebugMode { get; set; }
    public bool EnableWarpCommandForEveryone { get; set; }
    public bool EnableEnterSpecificMap { get; set; }
    public int MaxSpawnTime { get; set; }
    public bool DebugMapOnly { get; set; }
    public string DebugMapName { get; set; }
    public bool AddSimulatedLag { get; set; } = false;
    public int InboundSimulatedLag { get; set; } //in ms
    public int OutboundSimulatedLag { get; set; }
    //public bool UseForceEnterMap { get; set; }
    //public string ForceEnterMapName { get; set; }
}