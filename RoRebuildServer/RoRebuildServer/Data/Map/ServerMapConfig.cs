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

    public void AttachKillEvent(string spawnId, string name, int incAmnt) { }

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
                //the difference with the official server spawns is that each monster has its own spawn zone offset within the initial area randomly on startup.
                if (ServerConfig.OperationConfig.UseAccurateSpawnZoneFormula)
                {
                    var useStrict = spawn.UseStrictZone;
                    var zone = spawn.SpawnArea;
                    if (zone.IsZero) //if the monster has no spawn zone we use the map bounds minus 15 tiles around the outside
                    {
                        zone = Map.MapBounds.Shrink(14, 14);
                        useStrict = true;
                    }

                    if (zone.IsSingleCell)
                        useStrict = true;

                    if (useStrict)
                        Map.World.CreateMonster(Map, spawn.MonsterDatabaseInfo, zone, spawn);
                    else
                    {
                        var uniqueSpawn = spawn.Clone();
                        zone = zone.Shrink(1, 1);
                        uniqueSpawn.SpawnArea = Area.CreateAroundPoint(zone.RandomInArea(), zone.Width / 2, zone.Height / 2);
                        Map.World.CreateMonster(Map, spawn.MonsterDatabaseInfo, uniqueSpawn.SpawnArea, uniqueSpawn);
                    }
                }
                else
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


    public MapSpawnRule? CreateBoss(string mobName, int count, Area area, int respawn = 0, int respawn2 = 0)
    {
        var spawn = CreateSpawn(mobName, count, area, respawn, respawn2);
        if (spawn != null)
            spawn.UseStrictZone = true;
        return spawn;
    }

    public MapSpawnRule? CreateBoss(string mobName, int count, int respawn = 0, int variance = 0)
    {
        var spawn = CreateSpawn(mobName, count, respawn, variance);
        if (spawn != null)
            spawn.UseStrictZone = true;
        return spawn;
    }

}