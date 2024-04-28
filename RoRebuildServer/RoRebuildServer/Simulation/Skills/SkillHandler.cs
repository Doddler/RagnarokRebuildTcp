using System.Reflection;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;

namespace RoRebuildServer.Simulation.Skills
{
    public static class SkillHandler
    {
        private static SkillHandlerBase?[] handlers;
        private static SkillHandlerAttribute[] skillAttributes;

        public static SkillHandlerAttribute GetSkillAttributes(CharacterSkill skill) => skillAttributes[(int)skill];

        static SkillHandler()
        {
            var count = System.Enum.GetNames(typeof(CharacterSkill)).Length;
            handlers = new SkillHandlerBase[count];
            skillAttributes = new SkillHandlerAttribute[count];

            foreach(var type in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsClass && t.GetCustomAttribute<SkillHandlerAttribute>() != null))
            {
                var handler = (SkillHandlerBase)Activator.CreateInstance(type)!;
                var attr = type.GetCustomAttribute<SkillHandlerAttribute>();
                var skill = attr.SkillType;
                handler.SkillClassification = attr.SkillClassification;

                handlers[(int)skill] = handler;
                skillAttributes[(int)skill] = attr;
            }
        }

        public static SkillValidationResult ValidateTarget(SkillCastInfo info, CombatEntity src)
        {
            CombatEntity? target = info.TargetEntity.GetIfAlive<CombatEntity>();
            
            var handler = handlers[(int)info.Skill];
            if (handler != null)
                return handler.ValidateTarget(src, target, info.TargetedPosition);

            return SkillValidationResult.Failure;
        }
        
        public static float GetSkillCastTime(CharacterSkill skill, CombatEntity src, CombatEntity target, int level)
        {
            var handler = handlers[(int)skill];
            if (handler != null)
                return handler.GetCastTime(src, target, level);
            return 0f;
        }

        public static int GetSkillRange(CombatEntity src, CharacterSkill skill, int level)
        {
            var handler = handlers[(int)skill];
            if (handler != null)
            {
                var range = handler.GetSkillRange(src, level);
                if(range > 0)
                    return range;
            }

            return src.GetStat(CharacterStat.Range);
        }

        public static void ExecuteSkill(SkillCastInfo info, CombatEntity src)
        {
            CombatEntity? target = info.TargetEntity.GetIfAlive<CombatEntity>();
            var handler = handlers[(int)info.Skill];
            if (handler != null)
                handler.Process(src, target, info.TargetedPosition, info.Level);
        }
        
    }
}
