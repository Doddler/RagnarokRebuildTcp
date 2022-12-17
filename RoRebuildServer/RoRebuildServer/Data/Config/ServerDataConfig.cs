namespace RoRebuildServer.Data.Config;

public class ServerDataConfig
{
    public required string DataPath { get; set; }
    public required string CachePath { get; set; }
    public required bool CacheScripts { get; set; }
    public required bool CompileScriptsOutOfProcess { get; set; }
    public required string WalkPathData { get; set; }
}