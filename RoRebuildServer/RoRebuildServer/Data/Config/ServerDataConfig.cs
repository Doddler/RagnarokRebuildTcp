namespace RoRebuildServer.Data.Config;

public class ServerDataConfig
{
    public string DataPath { get; set; }
    public string CachePath { get; set; }
    public bool CacheScripts { get; set; }
    public bool CompileScriptsOutOfProcess { get; set; }
    public string WalkPathData { get; set; }
}