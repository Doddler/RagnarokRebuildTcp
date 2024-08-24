using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation;

namespace RoRebuildServer.Data.Map;

[Flags]
public enum SpawnCreateFlags
{
    None = 0,
    Boss = 1,
    GuaranteeInZone = 2,
    StrictArea = 4,
    LockToSpawnZone = 8,
    MVP = Boss | GuaranteeInZone | StrictArea,
    LockedArea = GuaranteeInZone | StrictArea | LockToSpawnZone
}

public class ServerMapConfig
{
    public Simulation.Map Map;
    public List<MapSpawnRule> SpawnRules { get; set; }

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
                        //zone = Map.MapBounds.Shrink(14, 14);
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

    public MapSpawnRule CreateSpawn(string mobName, int count, Area area, int respawn, int variance, SpawnCreateFlags flags = SpawnCreateFlags.None)
    {
        if (!DataManager.MonsterCodeLookup.TryGetValue(mobName.Replace(" ", "_").ToUpper(), out var mobStats))
        {
            ServerLogger.LogError($"Could not spawn monster with name of '{mobName}', name not found.");
            return null!;
        }

        if (respawn < ServerConfig.DebugConfig.MinSpawnTime)
            respawn = ServerConfig.DebugConfig.MinSpawnTime;

        area = area.ClipArea(Map.MapBounds);

        var displayType = CharacterDisplayType.Monster;
        if (flags.HasFlag(SpawnCreateFlags.Boss))
            displayType = CharacterDisplayType.Boss;
        if (flags.HasFlag(SpawnCreateFlags.MVP))
            displayType = CharacterDisplayType.Mvp;

        var spawn = new MapSpawnRule(SpawnRules.Count, mobStats, area, count, respawn, respawn + variance, displayType);
        if (!area.IsZero)
        {
            spawn.UseStrictZone = flags.HasFlag(SpawnCreateFlags.StrictArea);
            spawn.GuaranteeInZone = flags.HasFlag(SpawnCreateFlags.GuaranteeInZone);
        }

        SpawnRules.Add(spawn);

        if(flags.HasFlag(SpawnCreateFlags.LockToSpawnZone))
            spawn.LockToSpawnZone();
        
        //Console.WriteLine(mobName + "  BBB");
        return spawn;
    }

    public MapSpawnRule? CreateSpawn(string mobName, int count, int respawn, int variance, SpawnCreateFlags flags = SpawnCreateFlags.None) =>
        CreateSpawn(mobName, count, Area.Zero, respawn, variance, flags);
        

    public MapSpawnRule? CreateSpawn(string mobName, int count, Area area, int respawn, SpawnCreateFlags flags = SpawnCreateFlags.None) =>
        CreateSpawn(mobName, count, area, respawn, 0, flags);

    public MapSpawnRule? CreateSpawn(string mobName, int count, Area area, SpawnCreateFlags flags = SpawnCreateFlags.None) =>
        CreateSpawn(mobName, count, area, 0, 0, flags);

    public MapSpawnRule? CreateSpawn(string mobName, int count, int respawn, SpawnCreateFlags flags = SpawnCreateFlags.None) =>
        CreateSpawn(mobName, count, respawn, 0, flags);

    public MapSpawnRule? CreateSpawn(string mobName, int count, SpawnCreateFlags flags = SpawnCreateFlags.None) =>
        CreateSpawn(mobName, count, 0, 0, flags);

}