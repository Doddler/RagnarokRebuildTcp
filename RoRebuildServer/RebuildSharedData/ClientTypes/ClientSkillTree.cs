using System;
using System.Collections.Generic;
using System.Text;
using RebuildSharedData.Enum;

namespace RebuildSharedData.ClientTypes;

[Serializable]
public class ClientPrereq
{
    public CharacterSkill Skill;
    public int Level;
}

[Serializable]
public class ClientSkillTreeEntry
{
    public CharacterSkill Skill;
    public ClientPrereq[]? Prerequisites;
    
}

[Serializable]
public class ClientSkillTree
{
    public int ClassId;
    public int ExtendsClass;
    public int JobRank;
    public List<ClientSkillTreeEntry> Skills;
}