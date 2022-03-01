using RebuildSharedData.Data;
using RoRebuildServer.Data.Monster;

namespace RoRebuildServer.Data.Map;

public class MapSpawnRule
{
    public MonsterDatabaseInfo MonsterDatabaseInfo { get; set; }
    public Area SpawnArea { get; set; }
    public int Count { get; set; }
    public int MinSpawnTime { get; set; }
    public int MaxSpawnTime { get; set; }
    public bool HasSpawnZone { get; set; }

    public MapSpawnRule(MonsterDatabaseInfo monsterDatabaseInfo, Area spawnArea, int count, int minSpawnTime, int maxSpawnTime)
    {
        MonsterDatabaseInfo = monsterDatabaseInfo;
        SpawnArea = spawnArea;
        Count = count;
        MinSpawnTime = minSpawnTime;
        MaxSpawnTime = maxSpawnTime;
        HasSpawnZone = true;
    }
    
    public MapSpawnRule(MonsterDatabaseInfo monsterDatabaseInfo, int count, int minSpawnTime, int maxSpawnTime)
    {
        MonsterDatabaseInfo = monsterDatabaseInfo;
        HasSpawnZone = false;
        Count = count;
        MinSpawnTime = minSpawnTime;
        MaxSpawnTime = maxSpawnTime;
    }

}