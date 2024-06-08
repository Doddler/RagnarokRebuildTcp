namespace RebuildSharedData.ClientTypes;

#pragma warning disable CS8618 //disable warning for nullable fields

[Serializable]
public class MonsterClassData
{
    public int Id;
    public string Code;
    public string Name;
    public string SpriteName;
    public float Offset;
    public float ShadowSize;
    public float Size;
    public float AttackTiming;
}


[Serializable]
public class DatabaseMonsterClassData
{
    public List<MonsterClassData> MonsterClassData;
}