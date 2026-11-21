using RebuildSharedData.Enum.EntityStats;

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
    //race, size type and element are left at their defaults for npcs, which have none
    public CharacterRace Race;
    //small, medium or large; Size is the sprite's scale
    public CharacterSize SizeType;
    public CharacterElement Element;

    //public float HitTiming;
    public string Color = "FFFFFFFF";
}

[Serializable]
public class DatabaseMonsterClassData
{
    public List<MonsterClassData> MonsterClassData;
}