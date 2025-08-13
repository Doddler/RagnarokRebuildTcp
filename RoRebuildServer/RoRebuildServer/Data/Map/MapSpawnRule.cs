using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data.Monster;

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
    public bool UseStrictZone { get; set; } //on official servers the server drifts the origin positions of each monster from its spawn area, but do we want to do that here?
    public bool GuaranteeInZone { get; set; } //make sure the monster cannot spawn outside the selected zone.
    public CharacterDisplayType DisplayType { get; set; }

    public void LockToSpawnZone() => LockToSpawn = true;

    public MapSpawnRule(int id, MonsterDatabaseInfo monsterDatabaseInfo, Area spawnArea, int count, int minSpawnTime, int maxSpawnTime, 
        CharacterDisplayType displayType, bool useStrictZone = false, bool guaranteeInZone = false)
    {
        Id = id;
        MonsterDatabaseInfo = monsterDatabaseInfo;
        SpawnArea = spawnArea;
        Count = count;
        MinSpawnTime = minSpawnTime;
        MaxSpawnTime = maxSpawnTime;
        HasSpawnZone = true;
        UseStrictZone = useStrictZone;
        GuaranteeInZone = guaranteeInZone;
        DisplayType = displayType;
    }
    
    //public MapSpawnRule(int id, MonsterDatabaseInfo monsterDatabaseInfo, int count, int minSpawnTime, int maxSpawnTime, 
    //    bool useStrictZone = false, bool guaranteeInZone = false)
    //{
    //    Id = id;
    //    MonsterDatabaseInfo = monsterDatabaseInfo;
    //    HasSpawnZone = false;
    //    Count = count;
    //    MinSpawnTime = minSpawnTime;
    //    MaxSpawnTime = maxSpawnTime;
    //    UseStrictZone = useStrictZone;
    //    GuaranteeInZone = guaranteeInZone;
    //}

    public MapSpawnRule Clone() => new MapSpawnRule(Id, MonsterDatabaseInfo, SpawnArea, Count, MinSpawnTime, MaxSpawnTime, DisplayType, UseStrictZone, GuaranteeInZone);

}