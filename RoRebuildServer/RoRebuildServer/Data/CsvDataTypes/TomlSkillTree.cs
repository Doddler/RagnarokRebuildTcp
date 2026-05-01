using System.Collections;
using RebuildSharedData.Enum;

namespace RoRebuildServer.Data.CsvDataTypes;

public class SkillPrereq : IList<object>
{
    public CharacterSkill Skill;
    public int RequiredLevel;

    public void Add(object item)
    {
        if (item is string skill)
            Skill = Enum.Parse<CharacterSkill>(skill);
        if (item is long lvl)
            RequiredLevel = (int)lvl;
    }

    //we aren't actually using any of these things, we just need to be able to be usable to the Toml parser

    public IEnumerator<object> GetEnumerator() { throw new NotImplementedException(); }
    IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
    public void Clear() { throw new NotImplementedException(); }
    public bool Contains(object item) { throw new NotImplementedException(); }
    public void CopyTo(object[] array, int arrayIndex) { throw new NotImplementedException(); }
    public bool Remove(object item) { throw new NotImplementedException(); }
    public int Count { get; } = 0;
    public bool IsReadOnly { get; } = false;
    public int IndexOf(object item) { throw new NotImplementedException(); }
    public void Insert(int index, object item) { throw new NotImplementedException(); }
    public void RemoveAt(int index) { throw new NotImplementedException(); }

    public object this[int index]
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }
}

public class CsvPlayerSkillTree
{
    public int JobRank { get; set; }
    public string? Extends { get; set; }
    public Dictionary<CharacterSkill, List<SkillPrereq>?>? SkillTree { get; set; }
}

public class PlayerSkillTree
{
    public int JobRank { get; set; }
    public int JobId { get; set; }
    public int PrereqSkillPoints { get; set; }
    public PlayerSkillTree? Parent { get; set; }
    public Dictionary<CharacterSkill, List<SkillPrereq>?> SkillTree { get; set; }
}