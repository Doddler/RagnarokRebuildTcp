using RebuildSharedData.Data;
using RoRebuildServer.Data.Monster;
using RoRebuildServer.Logging;

namespace RoRebuildServer.Data.Map;

public class MapSpawnRule
{
    public MonsterDatabaseInfo MonsterDatabaseInfo { get; set; }
    public int Id { get; set; }
    public Area SpawnArea { get; set; }
    public int Count { get; set; }
    public int MinSpawnTime { get; set; }
    public int MaxSpawnTime { get; set; }
    public bool HasSpawnZone { get; set; }
    public bool LockToSpawn { get; set; }

    public void LockToSpawnZone() => LockToSpawn = true;

    public MapSpawnRule(int id, MonsterDatabaseInfo monsterDatabaseInfo, Area spawnArea, int count, int minSpawnTime, int maxSpawnTime)
    {
        Id = id;
        MonsterDatabaseInfo = monsterDatabaseInfo;
        SpawnArea = spawnArea;
        Count = count;
        MinSpawnTime = minSpawnTime;
        MaxSpawnTime = maxSpawnTime;
        HasSpawnZone = true;
    }
    
    public MapSpawnRule(int id, MonsterDatabaseInfo monsterDatabaseInfo, int count, int minSpawnTime, int maxSpawnTime)
    {
        Id = id;
        MonsterDatabaseInfo = monsterDatabaseInfo;
        HasSpawnZone = false;
        Count = count;
        MinSpawnTime = minSpawnTime;
        MaxSpawnTime = maxSpawnTime;
    }
    
}