using System;
using System.Collections.Generic;
using System.Text;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;

namespace RebuildSharedData.ClientTypes;

#nullable disable

[Serializable]
public class SkillData
{
    public CharacterSkill SkillId;
    public string Icon;
    public string Name;
    public SkillTarget Target;
    public int MaxLevel;
    public int[] SpCost;
    public bool AdjustableLevel;
}