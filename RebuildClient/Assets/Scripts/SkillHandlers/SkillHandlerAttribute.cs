using System;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers
{

    public class SkillHandlerAttribute : Attribute
    {
        public CharacterSkill SkillType;

        public SkillHandlerAttribute(CharacterSkill skillType)
        {
            SkillType = skillType;
        }
    }
}