using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Logging;

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

//while this seems pointless to have an interface for this, it exists so we can query map spawn data in an external app without an associated map
public interface IServerMapConfig
{
    List<MapSpawnRule> SpawnRules { get; set; }
    void AttachKillEvent(string spawnId, string name, int incAmnt);

    void CreateSpawnEvent(string ev, int interval, string mobname, int spawnPer, int maxSpawn,
        int respawnTime);

    void ApplySpawnsToMap();
    MapSpawnRule CreateSpawn(string mobName, int count, Area area, int respawn, int variance, SpawnCreateFlags flags = SpawnCreateFlags.None);
    MapSpawnRule? CreateSpawn(string mobName, int count, int respawn, int variance, SpawnCreateFlags flags = SpawnCreateFlags.None);
    MapSpawnRule? CreateSpawn(string mobName, int count, Area area, int respawn, SpawnCreateFlags flags = SpawnCreateFlags.None);
    MapSpawnRule? CreateSpawn(string mobName, int count, Area area, SpawnCreateFlags flags = SpawnCreateFlags.None);
    MapSpawnRule? CreateSpawn(string mobName, int count, int respawn, SpawnCreateFlags flags = SpawnCreateFlags.None);
    MapSpawnRule? CreateSpawn(string mobName, int count, SpawnCreateFlags flags = SpawnCreateFlags.None);
}

public class ServerMapConfig : IServerMapConfig
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

            if (!spawn.SpawnArea.IsZero && (spawn.SpawnArea.Size < 10 || (spawn.SpawnArea.Width <= 5 && spawn.SpawnArea.Height <= 5)) && spawn.MaxSpawnTime <= ServerConfig.DebugConfig.MinSpawnTime)
                ServerLogger.LogWarning($"Spawn of {spawn.Count}x {spawn.MonsterDatabaseInfo.Code} on map {Map.Name} is set to instantly respawn within a small area ({spawn.SpawnArea}). Is this intended?");

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

        var minTime = ServerConfig.DebugConfig.MinSpawnTime;
        var maxTime = ServerConfig.DebugConfig.MaxSpawnTime;

        if ( minTime > 0 && respawn < minTime)
            respawn = minTime;
        
        var respawnMax = respawn + variance;

        if (maxTime > 0 && respawn > maxTime)
            respawn = maxTime;
        if (maxTime > 0 && respawnMax > maxTime)
            respawnMax = maxTime;

        DataManager.ServerConfigScriptManager.SetMonsterSpawnTime(mobStats, Map.Name, ref respawn, ref respawnMax);
        
        area = area.ClipArea(Map.MapBounds);

        var displayType = CharacterDisplayType.Monster;
        if (flags.HasFlag(SpawnCreateFlags.Boss))
            displayType = CharacterDisplayType.Boss;
        if (flags.HasFlag(SpawnCreateFlags.MVP))
            displayType = CharacterDisplayType.Mvp;

        var spawn = new MapSpawnRule(SpawnRules.Count, mobStats, area, count, respawn, respawnMax, displayType);
        if (!area.IsZero)
        {
            spawn.UseStrictZone = flags.HasFlag(SpawnCreateFlags.StrictArea);
            spawn.GuaranteeInZone = flags.HasFlag(SpawnCreateFlags.GuaranteeInZone);
        }

        SpawnRules.Add(spawn);

        if(flags.HasFlag(SpawnCreateFlags.LockToSpawnZone))
            spawn.LockToSpawnZone();
        
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