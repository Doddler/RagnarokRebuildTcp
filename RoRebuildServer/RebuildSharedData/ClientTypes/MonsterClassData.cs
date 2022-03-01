namespace RebuildSharedData.ClientTypes;

[Serializable]
public class MonsterClassData
{
    public int Id;
    public string Name;
    public string SpriteName;
    public float Offset;
    public float ShadowSize;
    public float Size;
}


[Serializable]
public class DatabaseMonsterClassData
{
    public List<MonsterClassData> MonsterClassData;
}