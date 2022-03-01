using RebuildSharedData.Data;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation;

namespace RoRebuildServer.Data.Map;

public class ServerMapConfig
{
    public Simulation.Map Map;
    public List<MapSpawnRule> SpawnRules;

    public ServerMapConfig(Simulation.Map map)
    {
        Map = map;
        SpawnRules = new List<MapSpawnRule>(10);
    }

    public void AttachKillEvent(string spawnId, string name, int incAmnt) {}

    public void CreateSpawnEvent(string ev, int interval, string mobname, int spawnPer, int maxSpawn,
        int respawnTime)
    {

    }

    public int CreateSpawn(string mobName, int count, Area area, int respawn = 0, int respawn2 = 0)
    {
        if (!DataManager.MonsterCodeLookup.TryGetValue(mobName.Replace(" ", "_").ToUpper(), out var mobStats))
        {
            ServerLogger.LogError($"Could not spawn monster with name of '{mobName}', name not found.");
            return -1;
        }

        var spawn = new MapSpawnRule(mobStats, area, count, respawn, respawn + respawn2);
        SpawnRules.Add(spawn);

        for (var i = 0; i < count; i++)
            Map.World.CreateMonster(Map, mobStats, 0, 0, 0, 0, spawn);

        //Console.WriteLine(mobName + "  BBB");
        return SpawnRules.Count - 1;
    }

    public int CreateSpawn(string mobName, int count, int respawn = 0, int variance = 0)
    {
        if (!DataManager.MonsterCodeLookup.TryGetValue(mobName.ToUpper(), out var mobStats))
        {
            ServerLogger.LogError($"Could not spawn monster with name of '{mobName}', name not found.");
            return -1;
        }

        var spawn = new MapSpawnRule(mobStats, count, respawn, respawn + variance);
        SpawnRules.Add(spawn);

        for(var i = 0; i < count; i++)
            Map.World.CreateMonster(Map, mobStats, 0, 0, 0, 0, spawn);

        //Console.WriteLine(mobName + "  BBB");
        return SpawnRules.Count - 1;
    }
    
}