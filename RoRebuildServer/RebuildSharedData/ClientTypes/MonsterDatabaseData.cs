namespace RebuildSharedData.ClientTypes;

#pragma warning disable CS8618 //disable warning for nullable fields

[Serializable]
public class MonsterDbDropEntry
{
    public int ItemId;
    public int Chance;
    public int CountMin;
    public int CountMax;
}

[Serializable]
public class MonsterDbSpawnEntry
{
    public string Map;
    public int Count;
    public int RespawnMin;
    public int RespawnMax;
    public bool IsBoss;
    public bool IsMvp;
}

[Serializable]
public class MonsterDbEntry
{
    public int Id;
    public string Code;
    public string Name;
    public int Level;
    public int HP;
    public int Exp;
    public int JExp;
    public int AtkMin;
    public int AtkMax;
    public int Def;
    public int MDef;
    public int Str;
    public int Agi;
    public int Vit;
    public int Int;
    public int Dex;
    public int Luk;
    public int Range;
    public int ScanDist;
    public float MoveSpeed;
    public string Size;
    public string Element;
    public string Race;
    public string Ai;
    public string Special;
    public List<string> Tags;
    public List<MonsterDbDropEntry> Drops;
    public List<MonsterDbSpawnEntry> Spawns;
}

[Serializable]
public class MonsterDbFile
{
    public List<MonsterDbEntry> Items;
}
