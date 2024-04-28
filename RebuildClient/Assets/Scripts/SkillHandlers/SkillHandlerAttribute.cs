using System;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers
{

    public class SkillHandlerAttribute : Attribute
    {
        public CharacterSkill SkillType;
        public bool RunHandlerWithoutSource;

        public SkillHandlerAttribute(CharacterSkill skillType, bool ExecuteWithoutSource = false)
        {
            SkillType = skillType;
            RunHandlerWithoutSource = ExecuteWithoutSource;
        }
    }
}