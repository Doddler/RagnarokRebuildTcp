using System;
using System.Collections.Generic;
using System.Text;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;

namespace RebuildSharedData.ClientTypes
{
    [Serializable]
    public class SkillData
    {
        public CharacterSkill SkillId;
        public SkillTarget Type;
        public string Name;
        public int MaxLevel;
        public bool CanAdjustLevel;
    }
    
}

