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
    
    public void ApplySpawnsToMap()
    {
        for (var i = 0; i < SpawnRules.Count; i++)
        {
            var spawn = SpawnRules[i];

            for (var j = 0; j < spawn.Count; j++)
            {
                Map.World.CreateMonster(Map, spawn.MonsterDatabaseInfo, spawn.SpawnArea, spawn);
            }
        }
    }

    public MapSpawnRule? CreateSpawn(string mobName, int count, Area area, int respawn = 0, int respawn2 = 0)
    {
        if (!DataManager.MonsterCodeLookup.TryGetValue(mobName.Replace(" ", "_").ToUpper(), out var mobStats))
        {
            ServerLogger.LogError($"Could not spawn monster with name of '{mobName}', name not found.");
            return null;
        }

        area = area.ClipArea(Map.MapBounds);

        var spawn = new MapSpawnRule(SpawnRules.Count, mobStats, area, count, respawn, respawn + respawn2);
        SpawnRules.Add(spawn);
        
        //Console.WriteLine(mobName + "  BBB");
        return spawn;
    }

    public MapSpawnRule? CreateSpawn(string mobName, int count, int respawn = 0, int variance = 0)
    {
        if (!DataManager.MonsterCodeLookup.TryGetValue(mobName.ToUpper(), out var mobStats))
        {
            ServerLogger.LogError($"Could not spawn monster with name of '{mobName}', name not found.");
            return null;
        }

        var spawn = new MapSpawnRule(SpawnRules.Count, mobStats, count, respawn, respawn + variance);
        SpawnRules.Add(spawn);
        
        //Console.WriteLine(mobName + "  BBB");
        return spawn;
    }
    
}